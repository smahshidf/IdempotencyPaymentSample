using MongoDB.Driver;
using PaymentSystem.Consumer.Entities;
using PaymentSystem.Consumer.Inbox;
using PaymentSystem.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace PaymentSystem.Consumer;

public class ProcessMessage : BackgroundService 
{
    private AsyncEventingBasicConsumer consumer;
    private IChannel _channel;
    private readonly ILogger<ProcessMessage> _logger;
    private readonly IPaymentRepository _paymentRepository;
    private IConnection? _connection;
    private readonly IServiceProvider _serviceProvider;
    
    public ProcessMessage(
        ILogger<ProcessMessage> logger,
        IPaymentRepository paymentRepository,
        IServiceProvider serviceProvider,
        IConnection? connection)
    {
        _logger = logger;
        _paymentRepository = paymentRepository;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        var queueName = "payment-requested-queue";
        _connection = _serviceProvider.GetRequiredService<IConnection>();
        _channel = await _connection.CreateChannelAsync();

        await _channel.ExchangeDeclareAsync(
            exchange: "payment",
            type: ExchangeType.Topic,
            durable: true);

        await _channel.QueueDeclareAsync(
            queue: "payment-requested-queue",
            durable: true,
            exclusive: false,
            autoDelete: false);

        await _channel.QueueBindAsync(
            queue: "payment-requested-queue",
            exchange: "payment",
            routingKey: "payment.requested");

        consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += IdempotentProcessMessageAsync;

        await _channel.BasicConsumeAsync(
            queue: "payment-requested-queue",
            autoAck: false,
            consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        consumer.ReceivedAsync -= IdempotentProcessMessageAsync;
        _channel?.Dispose();
    }

    private async Task ProcessMessageAsync(object? sender, BasicDeliverEventArgs evt)
    {
        var body = evt.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var paymentRequested = JsonSerializer.Deserialize<PaymentRequestedEvent>(message);
        _logger.LogInformation("Processing payment for UserId: {UserId}, Amount: {Amount}", paymentRequested.UserId, paymentRequested.Amount);

        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            Amount = paymentRequested.Amount,
            Currency = paymentRequested.Currency,
            UserId = paymentRequested.UserId,
            Status = paymentRequested.PaymentStatus.ToString(),
            ProcessedAtUtc = DateTime.UtcNow
        };

        await _paymentRepository.CreateAsync(payment);
        _logger.LogInformation("Payment processed for OrderId: {OrderId}, PaymentId: {PaymentId}", payment.UserId, payment.Id);
    }



    private async Task IdempotentProcessMessageAsync(object? sender, BasicDeliverEventArgs evt)
    {
        using var scope = _serviceProvider.CreateScope();
        var inboxPersister = scope.ServiceProvider.GetRequiredService<IInboxPersister>();
        //var mongoClient = scope.ServiceProvider.GetRequiredService<IMongoClient>();

        var body = evt.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var paymentRequested = JsonSerializer.Deserialize<PaymentRequestedEvent>(message);

        if (!await inboxPersister.TryInsertAsync(paymentRequested.MessageId))
        {
            _logger.LogInformation("Duplicate message skipped. MessageId: {MessageId}", paymentRequested.MessageId);
            _channel!.BasicAckAsync(evt.DeliveryTag, multiple: false);
            return;
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            UserId = paymentRequested.UserId,
            Amount = paymentRequested.Amount,
            Currency = paymentRequested.Currency,
            Status = paymentRequested.PaymentStatus.ToString(),
            ProcessedAtUtc = DateTime.UtcNow
        };

        //using var session = await mongoClient.StartSessionAsync();
        //session.StartTransaction();

        try
        {
            await _paymentRepository.CreateAsync(payment);

            await inboxPersister.MarkProcessedAsync(paymentRequested.MessageId);

            //await session.CommitTransactionAsync();

            await _channel!.BasicAckAsync(evt.DeliveryTag, multiple: false);
            _logger.LogInformation("Successfully processed and recorded message: {MessageId}", paymentRequested.MessageId);
        }
        catch (Exception ex)
        {

            //await session.AbortTransactionAsync();
            _logger.LogError(ex, "Transaction failed for message {Id}. Rolling back.", paymentRequested.MessageId);

            await _channel!.BasicNackAsync(evt.DeliveryTag, false, requeue: true);
        }       
    }
}
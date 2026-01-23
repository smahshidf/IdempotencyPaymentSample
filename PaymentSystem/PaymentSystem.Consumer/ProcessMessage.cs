using System.Text;
using System.Text.Json;
using MongoDB.Driver;
using PaymentSystem.Consumer.Entities;
using PaymentSystem.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentSystem.Consumer;

public class ProcessMessage : BackgroundService 
{
    private AsyncEventingBasicConsumer consumer;
    private readonly IChannel _channel;
    private readonly ILogger<ProcessMessage> _logger;
    private readonly IPaymentRepository _paymentRepository;
    private IConnection? _connection;
    private readonly IServiceProvider _serviceProvider;
    public ProcessMessage(ILogger<ProcessMessage> logger, IPaymentRepository paymentRepository, IServiceProvider serviceProvider, IConnection? connection)
    {
        _logger = logger;
        _paymentRepository = paymentRepository;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        var queueName = "payment-requested-queue";
        _connection = _serviceProvider.GetRequiredService<IConnection>();

        await using var channel = await _connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: "payment",
            type: ExchangeType.Topic,
            durable: true);

        await channel.QueueDeclareAsync(
            queue: "payment-requested-queue",
            durable: true,
            exclusive: false,
            autoDelete: false);

        await channel.QueueBindAsync(
            queue: "payment-requested-queue",
            exchange: "payment",
            routingKey: "payment.requested");

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += ProcessMessageAsync;

        await channel.BasicConsumeAsync(
            queue: "payment-requested-queue",
            autoAck: false,
            consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        consumer.ReceivedAsync -= ProcessMessageAsync;
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
            PaymentId = paymentRequested.PaymentId,
            Amount = paymentRequested.Amount,
            Currency = paymentRequested.Currency,
            UserId = paymentRequested.UserId,
            Status = paymentRequested.PaymentStatus.ToString(),
            ProcessedAtUtc = DateTime.UtcNow
        };

        await _paymentRepository.CreateAsync(payment);
        _logger.LogInformation("Payment processed for OrderId: {OrderId}, PaymentId: {PaymentId}", payment.UserId, payment.Id);
    }
}
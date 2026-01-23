using System.Text;
using System.Text.Json;
using PaymentSystem.Contracts;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMQClient("rabbitMq");

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/pay", async (
    PaymentRequest request,
    IConnection connection) =>
{
    await using var channel = await connection.CreateChannelAsync();

    await channel.ExchangeDeclareAsync(
        exchange: "payment",
        type: ExchangeType.Topic,
        durable: true);

    await channel.QueueDeclareAsync(
        queue: "payment-requested-queue",
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null);

    await channel.QueueBindAsync(
        queue: "payment-requested-queue",
        exchange: "payment",
        routingKey: "payment.requested");

    var evt = new PaymentRequestedEvent(
        "3cf7ffdf-ccec-4ab8-b711-99d63d474ffa",
        Amount: request.Amount,
        Currency: request.Currency,
        UserId: request.UserId,
        PaymentStatus: Enum.Parse<PaymentStatus>(request.Status, ignoreCase: true),
        RequestedAt: DateTime.UtcNow);

    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));

    await channel.BasicPublishAsync(
        exchange: "payment",
        routingKey: "payment.requested",
        mandatory: false,
        basicProperties: new BasicProperties { Persistent = true },
        body: body);

    return Results.Accepted(value: new { PaymentId = evt.UserId });
});


app.Run();

record PaymentRequest(string UserId, decimal Amount, string Currency, string Status);

using PaymentSystem.Consumer;
using PaymentSystem.Consumer.Entities;
using PaymentSystem.Consumer.Mongo;
using RabbitMQ.Client;


var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

//RabbitMQ
builder.AddRabbitMQClient("rabbitMq");

// MongoDB
builder.Services.AddMongoDb(builder.Configuration);
builder.Services.AddSingleton<IPaymentRepository, PaymentRepository>();

builder.Services.AddHostedService<ProcessMessage>();

var app = builder.Build();
app.Run();
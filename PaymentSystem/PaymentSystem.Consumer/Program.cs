using PaymentSystem.Consumer;
using PaymentSystem.Consumer.Entities;
using PaymentSystem.Consumer.Inbox;
using PaymentSystem.Consumer.Mongo;


var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

//RabbitMQ
builder.AddRabbitMQClient("rabbitMq");

// MongoDB
builder.Services.AddMongoDb(builder.Configuration);
builder.Services.AddSingleton<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IInboxPersister, InboxPersister>();

builder.Services.AddHostedService<ProcessMessage>();

var app = builder.Build();
app.Run();
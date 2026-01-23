using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.PaymentSystem_ProviderApi>("paymentsystem-providerapi")
    .WithHttpEndpoint(5001, name: "public")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);

builder.AddProject<Projects.PaymentSystem_Consumer>("paymentsystem-consumer")
    .WithHttpEndpoint(5002, name: "public")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);

builder.Build().Run();

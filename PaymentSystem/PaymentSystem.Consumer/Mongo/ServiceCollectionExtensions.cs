
using MongoDB.Driver;

namespace PaymentSystem.Consumer.Mongo;

public static class MongoServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(
            configuration.GetSection("MongoDb"));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = configuration
                .GetSection("MongoDb")
                .Get<MongoDbSettings>();

            return new MongoClient(settings!.ConnectionString);
        });

        services.AddScoped<IMongoDatabase>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var settings = configuration
                .GetSection("MongoDb")
                .Get<MongoDbSettings>();

            return client.GetDatabase(settings!.DatabaseName);
        });

        return services;
    }
}

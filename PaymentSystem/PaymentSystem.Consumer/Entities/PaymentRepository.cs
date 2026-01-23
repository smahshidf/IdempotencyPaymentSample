using MongoDB.Driver;

namespace PaymentSystem.Consumer.Entities;

public interface IPaymentRepository
{
    Task CreateAsync(Payment payment);
}

public class PaymentRepository: IPaymentRepository
{
    private readonly IMongoCollection<Payment> _payments;
    public PaymentRepository(IMongoClient mongoClient)
    {
        _payments = mongoClient
            .GetDatabase("paymentsystemdb")
            .GetCollection<Payment>("payments");
    }

    public async Task CreateAsync(Payment payment)
    {
        try
        {
            await _payments.InsertOneAsync(payment);
        }
        catch (Exception ex)
        {
            Console.Write(ex.Message);
        }
    }
}

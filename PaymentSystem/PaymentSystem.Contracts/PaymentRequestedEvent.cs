
namespace PaymentSystem.Contracts
{
   public record PaymentRequestedEvent
    (   
        string MessageId,
        decimal Amount,
        string Currency,
        string UserId,
        PaymentStatus PaymentStatus,
        DateTime RequestedAt
    );

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed,
        Cancelled
    }
}

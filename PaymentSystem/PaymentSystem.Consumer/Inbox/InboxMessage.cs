using MongoDB.Bson.Serialization.Attributes;

namespace PaymentSystem.Consumer.Inbox;

public sealed class InboxMessage
{
    [BsonId]
    public string MessageId { get; set; } = default!;

    public DateTime ReceivedAtUtc { get; set; }
    public bool Processed { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}

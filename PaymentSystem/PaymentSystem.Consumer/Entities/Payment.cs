using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PaymentSystem.Consumer.Entities;

public sealed class Payment
{
    [BsonId]
    public required string Id { get; set; }
    public required string UserId { get; set; } = default!;
    public required decimal Amount { get; set; }
    public required string Currency { get; set; } = default!;
    public required string Status { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}

using MongoDB.Driver;

namespace PaymentSystem.Consumer.Inbox;

public interface IInboxPersister
{
    /// <summary>
    /// Attempts to register a message for processing.
    /// Returns false if the message already exists.
    /// </summary>
    Task<bool> TryInsertAsync(string messageId);

    /// <summary>
    /// Marks the message as successfully processed.
    /// </summary>
    Task MarkProcessedAsync(string messageId);
}

public sealed class InboxPersister : IInboxPersister
{
    private readonly IMongoCollection<InboxMessage> _collection;

    public InboxPersister(IMongoClient mongoClient)
    {
        _collection = mongoClient
            .GetDatabase("paymentsystemdb")
            .GetCollection<InboxMessage>("inbox");
    }

    public async Task<bool> TryInsertAsync(string messageId)
    {
        try
        {
            await _collection.InsertOneAsync(new InboxMessage
            {
                MessageId = messageId,
                ReceivedAtUtc = DateTime.UtcNow,
                Processed = false
            });

            return true;
        }
        catch (MongoWriteException ex)
            when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task MarkProcessedAsync(string messageId)
    {
        await _collection.UpdateOneAsync(
            x => x.MessageId == messageId,
            Builders<InboxMessage>.Update
                .Set(x => x.Processed, true)
                .Set(x => x.ProcessedAtUtc, DateTime.UtcNow));
    }
}

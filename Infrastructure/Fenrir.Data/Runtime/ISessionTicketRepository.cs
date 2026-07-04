namespace Fenrir.Data.Runtime;

/// <summary>Abstraction over Fenrir.Data.Runtime.SessionTicketRepository for DI/testability.</summary>
public interface ISessionTicketRepository
{
    public ValueTask CreateAsync(int accountId, int characterId, byte shardId, int ttlSeconds, CancellationToken ct);

    public ValueTask<ConsumedTicketDto?> ConsumeAsync(int accountId, CancellationToken ct);

    public ValueTask PurgeExpiredAsync(CancellationToken ct);
}

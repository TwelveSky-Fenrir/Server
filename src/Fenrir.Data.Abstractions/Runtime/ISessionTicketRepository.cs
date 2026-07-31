namespace Fenrir.Data.Abstractions.Runtime;

public interface ISessionTicketRepository
{
    public ValueTask CreateAsync(int accountId, int characterId, byte shardId, int ttlSeconds, Guid sessionToken,
        short accountGrade, short targetMapId, CancellationToken ct);

    public ValueTask<ConsumedTicketDto?> ConsumeAsync(int accountId, CancellationToken ct);

    public ValueTask PurgeExpiredAsync(CancellationToken ct);
}

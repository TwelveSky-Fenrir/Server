using System.Net;

namespace Fenrir.Data.Abstractions.Runtime;

public interface ISessionTicketRepository
{
    public ValueTask<bool> CreateAsync(int accountId, int characterId, byte shardId,
        int ttlSeconds, Guid sessionToken, short accountGrade, short targetMapId, IPAddress sourceAddress,
        CancellationToken ct);

    public ValueTask<ConsumedTicketDto?> ConsumeAsync(int accountId, byte expectedShardId,
        short expectedTargetMapId, IPAddress sourceAddress, CancellationToken ct);

    public ValueTask RevokeAsync(int accountId, CancellationToken ct);

    public ValueTask PurgeExpiredAsync(CancellationToken ct);
}

using System.Collections.Immutable;

namespace Fenrir.Data.Runtime;

/// <summary>Abstraction over Fenrir.Data.Runtime.GameServerDirectoryRepository for DI/testability.</summary>
public interface IGameServerDirectoryRepository
{
    public ValueTask HeartbeatAsync(byte shardId, string host, int port, int ccu, int capacity, float tickP99Ms,
        CancellationToken ct);

    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(CancellationToken ct);
}

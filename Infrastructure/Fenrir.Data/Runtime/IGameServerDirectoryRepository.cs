using System.Collections.Immutable;

namespace Fenrir.Data.Runtime;

public interface IGameServerDirectoryRepository
{
    public ValueTask HeartbeatAsync(byte shardId, string host, int port, int ccu, int capacity, float tickP99Ms,
        CancellationToken ct);

    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(CancellationToken ct);
}

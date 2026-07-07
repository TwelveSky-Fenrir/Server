using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for IGameServerDirectoryRepository: HeartbeatAsync is a GameServer-side concern, never
// exercised from Login handlers.
internal sealed class FakeGameServerDirectoryRepository(params ShardDirectoryEntryDto[] shards)
    : IGameServerDirectoryRepository
{
    /// <summary>
    ///     Every ShardId MarkUnreachableAsync was called with, in call order -- for ZoneTransferService's
    ///     dead-shard-eviction path (a failed reachability probe).
    /// </summary>
    public List<byte> MarkedUnreachableShardIds { get; } = [];

    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(ImmutableArray.Create(shards));
    }

    // No staleness modeling in this fake -- the fixed shard list passed to the constructor is always "live".
    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(int stalenessCutoffSeconds,
        CancellationToken ct)
    {
        return ValueTask.FromResult(ImmutableArray.Create(shards));
    }

    public ValueTask HeartbeatAsync(byte shardId, string host, int port, int ccu, int capacity, float tickP99Ms,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask MarkUnreachableAsync(byte shardId, CancellationToken ct)
    {
        MarkedUnreachableShardIds.Add(shardId);
        return ValueTask.CompletedTask;
    }
}

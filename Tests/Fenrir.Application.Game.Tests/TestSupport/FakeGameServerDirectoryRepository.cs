using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

// In-memory stand-in for IGameServerDirectoryRepository, mirroring
// Fenrir.Application.Login.Tests.TestSupport's own fake of the same interface: HeartbeatAsync is exercised via
// GameServerDirectoryHeartbeat (Hosting), never from a *.Services unit test.
internal sealed class FakeGameServerDirectoryRepository(params ShardDirectoryEntryDto[] shards)
    : IGameServerDirectoryRepository
{
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

    // MarkUnreachableAsync is a ZoneTransferService (Login.Services) concern, never exercised from a
    // *.Services unit test in this project.
    public ValueTask MarkUnreachableAsync(byte shardId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}

using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for IGameServerDirectoryRepository: HeartbeatAsync is a GameServer-side concern, never
// exercised from Login handlers.
internal sealed class FakeGameServerDirectoryRepository(params ShardDirectoryEntryDto[] shards)
    : IGameServerDirectoryRepository
{
    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(ImmutableArray.Create(shards));
    }

    public ValueTask HeartbeatAsync(byte shardId, string host, int port, int ccu, int capacity, float tickP99Ms,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}

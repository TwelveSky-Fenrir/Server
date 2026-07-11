using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeGameServerDirectoryRepository(params ShardDirectoryEntryDto[] shards)
    : IGameServerDirectoryRepository
{
    public List<byte> MarkedUnreachableShardIds { get; } = [];

    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(ImmutableArray.Create(shards));
    }

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

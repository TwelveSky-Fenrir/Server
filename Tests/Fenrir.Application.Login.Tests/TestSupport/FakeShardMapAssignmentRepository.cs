using Fenrir.Data.Admin;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for IShardMapAssignmentRepository, keyed exactly like admin.ShardMapAssignments (one
// disjoint list of MapIds per ShardId).
internal sealed class FakeShardMapAssignmentRepository(IReadOnlyDictionary<byte, short[]> hostedMapsByShard)
    : IShardMapAssignmentRepository
{
    public ValueTask<IReadOnlyList<short>> GetHostedMapsAsync(byte shardId, CancellationToken ct)
    {
        IReadOnlyList<short> maps = hostedMapsByShard.TryGetValue(shardId, out var mapIds) ? mapIds : [];
        return ValueTask.FromResult(maps);
    }
}

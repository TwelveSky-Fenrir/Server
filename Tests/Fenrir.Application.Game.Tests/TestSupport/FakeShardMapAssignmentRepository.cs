using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Application.Game.Tests.TestSupport;

// In-memory stand-in for IShardMapAssignmentRepository, mirroring Fenrir.Application.Login.Tests.TestSupport's
// own fake of the same interface: keyed exactly like admin.ShardMapAssignments (one disjoint list of MapIds
// per ShardId).
internal sealed class FakeShardMapAssignmentRepository(IReadOnlyDictionary<byte, short[]> hostedMapsByShard)
    : IShardMapAssignmentRepository
{
    public ValueTask<IReadOnlyList<short>> GetHostedMapsAsync(byte shardId, CancellationToken ct)
    {
        IReadOnlyList<short> maps = hostedMapsByShard.TryGetValue(shardId, out var mapIds) ? mapIds : [];
        return ValueTask.FromResult(maps);
    }

    public ValueTask<IReadOnlyList<ShardMapAssignmentDto>> GetAllAssignmentsAsync(CancellationToken ct)
    {
        IReadOnlyList<ShardMapAssignmentDto> rows = hostedMapsByShard
            .SelectMany(entry => entry.Value.Select(mapId => new ShardMapAssignmentDto(entry.Key, mapId)))
            .ToArray();
        return ValueTask.FromResult(rows);
    }
}

namespace Fenrir.Data.Abstractions.Admin;

public interface IShardMapAssignmentRepository
{
    public ValueTask<IReadOnlyList<short>> GetHostedMapsAsync(byte shardId, CancellationToken ct);

    public ValueTask<IReadOnlyList<ShardMapAssignmentDto>> GetAllAssignmentsAsync(CancellationToken ct);
}

namespace Fenrir.Data.Admin;

public interface IShardMapAssignmentRepository
{
    /// <summary>Map ids assigned to this shard (admin.ShardMapAssignments), ascending. Empty for an unconfigured shard.</summary>
    public ValueTask<IReadOnlyList<short>> GetHostedMapsAsync(byte shardId, CancellationToken ct);
}

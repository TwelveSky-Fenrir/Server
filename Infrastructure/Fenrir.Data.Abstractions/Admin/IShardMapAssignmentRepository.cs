namespace Fenrir.Data.Abstractions.Admin;

public interface IShardMapAssignmentRepository
{
    /// <summary>Map ids assigned to this shard (admin.ShardMapAssignments), ascending. Empty for an unconfigured shard.</summary>
    public ValueTask<IReadOnlyList<short>> GetHostedMapsAsync(byte shardId, CancellationToken ct);

    /// <summary>
    ///     Every (ShardId, MapId) row currently in admin.ShardMapAssignments, independent of which shards are
    ///     live in runtime.GameServerDirectory. Exists specifically so a boot-time overlap check can see a
    ///     colliding shard's claim even when that shard has never heartbeated yet -- e.g. every shard in a
    ///     fleet cold-booting at the same instant -- rather than only shards <see cref="GetHostedMapsAsync" />
    ///     could be asked about because the live directory already lists them (see ShardPartitionGuard).
    /// </summary>
    public ValueTask<IReadOnlyList<ShardMapAssignmentDto>> GetAllAssignmentsAsync(CancellationToken ct);
}

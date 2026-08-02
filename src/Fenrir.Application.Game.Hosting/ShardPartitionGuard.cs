using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Hosting;

public static class ShardPartitionGuard
{
    // Called twice: once at boot (Program.cs, thisShardId's own freshly-loaded hostedMaps, throws on
    // conflict) and once per heartbeat tick thereafter (GameServerDirectoryHeartbeat, thisShardId's own
    // FROZEN boot-time hostedMaps, caller logs instead of throwing). Only the second form can catch a map
    // reassigned away from thisShardId without a restart: for every OTHER shard this method only ever
    // trusts a fresh admin.ShardMapAssignments read (there is no cross-process channel today publishing
    // what a shard actually loaded, only what it is currently assigned), so a shard whose assignment
    // changed out from under it while still running is invisible to any OTHER shard's own boot-time check.
    public static async Task EnsureNoOverlapAsync(byte thisShardId, IReadOnlyCollection<short> hostedMaps,
        IGameServerDirectoryRepository directory, IShardMapAssignmentRepository shardMapAssignments,
        CancellationToken ct)
    {
        var liveShards = await directory.GetDirectoryAsync(ct);

        var claims = new List<ShardMapPartitionValidator.ShardMapClaim>
        {
            new(thisShardId, hostedMaps)
        };

        var claimedShardIds = new HashSet<byte> { thisShardId };

        foreach (var shard in liveShards)
        {
            if (!claimedShardIds.Add(shard.ShardId))
                continue;

            var otherHostedMaps = await shardMapAssignments.GetHostedMapsAsync(shard.ShardId, ct);
            claims.Add(new ShardMapPartitionValidator.ShardMapClaim(shard.ShardId, otherHostedMaps));
        }

        var allAssignments = await shardMapAssignments.GetAllAssignmentsAsync(ct);
        foreach (var group in allAssignments.GroupBy(assignment => assignment.ShardId))
        {
            if (!claimedShardIds.Add(group.Key))
                continue;

            claims.Add(new ShardMapPartitionValidator.ShardMapClaim(group.Key,
                group.Select(assignment => assignment.MapId).ToArray()));
        }

        var conflicts = ShardMapPartitionValidator.FindConflicts(claims)
            .Where(conflict => conflict.ShardIdA == thisShardId || conflict.ShardIdB == thisShardId)
            .ToArray();

        if (conflicts.Length == 0)
            return;

        var detail = string.Join("; ", conflicts.Select(conflict =>
        {
            var otherShardId = conflict.ShardIdA == thisShardId ? conflict.ShardIdB : conflict.ShardIdA;
            return $"shard {otherShardId} already claims map(s) [{string.Join(',', conflict.MapIds)}]";
        }));

        throw new InvalidOperationException(
            $"Shard {thisShardId} violates ADR-0012's disjoint-map-partition invariant: {detail}. Two " +
            "GameServer instances must never host the same map. Fix admin.ShardMapAssignments and/or stop the " +
            "conflicting shard -- if this was thrown from Program.cs boot, the process will now exit without " +
            "starting; if thrown from GameServerDirectoryHeartbeat's post-boot re-check, the caller logs this " +
            "instead of letting it propagate.");
    }
}

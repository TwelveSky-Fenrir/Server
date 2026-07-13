using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Hosting;

public static class ShardPartitionGuard
{
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
            $"Shard {thisShardId} cannot start: {detail}. ADR-0012 requires a shard to be a disjoint map " +
            "partition -- two GameServer instances must never host the same map. Fix admin.ShardMapAssignments " +
            "and/or stop the conflicting shard before retrying.");
    }
}

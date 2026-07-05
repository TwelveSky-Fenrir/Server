using Fenrir.Application.Game.World;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.GameServer;

/// <summary>
///     Boot-time enforcement of ADR-0012 rule 1 ("a shard is a disjoint map partition, never a replica"):
///     before this shard's <see cref="ZoneRegistry" /> is built and connections are accepted, confirms no
///     other currently-live shard already claims one of the maps this shard is about to host.
/// </summary>
public static class ShardPartitionGuard
{
    /// <summary>
    ///     Crosses <c>runtime.GameServerDirectory</c> (who is alive right now -- <paramref name="directory" />
    ///     already excludes stale heartbeats) with each live shard's own <c>admin.ShardMapAssignments</c> row
    ///     (<paramref name="shardMapAssignments" />) to build the claim list <see cref="ShardMapPartitionValidator" />
    ///     needs, then throws with a precise, actionable message if any live shard's claim collides with this
    ///     shard's <paramref name="hostedMaps" />. A conflict between two OTHER live shards (neither of them
    ///     this one) is not this process's boot to fail over -- it is reported by whichever of those two shards
    ///     boots next.
    /// </summary>
    public static async Task EnsureNoOverlapAsync(byte thisShardId, IReadOnlyCollection<short> hostedMaps,
        IGameServerDirectoryRepository directory, IShardMapAssignmentRepository shardMapAssignments,
        CancellationToken ct)
    {
        var liveShards = await directory.GetDirectoryAsync(ct);

        var claims = new List<ShardMapPartitionValidator.ShardMapClaim>
        {
            new(thisShardId, hostedMaps)
        };

        foreach (var shard in liveShards)
        {
            // Same shard id restarting fast enough to still see its own previous heartbeat row; hostedMaps
            // above (this boot's fresh read of admin.ShardMapAssignments) is authoritative for it, not a
            // second, redundant claim built from the same table.
            if (shard.ShardId == thisShardId)
                continue;

            var otherHostedMaps = await shardMapAssignments.GetHostedMapsAsync(shard.ShardId, ct);
            claims.Add(new ShardMapPartitionValidator.ShardMapClaim(shard.ShardId, otherHostedMaps));
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

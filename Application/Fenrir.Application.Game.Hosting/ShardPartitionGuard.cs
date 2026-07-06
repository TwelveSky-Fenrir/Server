using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Boot-time enforcement of ADR-0012 rule 1 ("a shard is a disjoint map partition, never a replica"):
///     before this shard's <see cref="ZoneRegistry" /> is built and connections are accepted, confirms no
///     other shard -- live right now, or merely configured in <c>admin.ShardMapAssignments</c> but not yet
///     live -- already claims one of the maps this shard is about to host.
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
    /// <remarks>
    ///     The live-directory crossing above cannot see a shard that has never heartbeated -- on a whole
    ///     fleet cold-booting at the same instant, every shard's own heartbeat only starts after this same
    ///     check passes (see <c>Program.cs</c>), so <paramref name="directory" /> can be empty for every
    ///     shard's check simultaneously, regardless of what <c>admin.ShardMapAssignments</c> actually
    ///     contains. <see cref="IShardMapAssignmentRepository.GetAllAssignmentsAsync" /> closes that gap: it
    ///     reads every (ShardId, MapId) row in the assignment table directly, independent of liveness, so a
    ///     shard identifier present in the table but not yet live still contributes a claim here.
    /// </remarks>
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
            // Same shard id restarting fast enough to still see its own previous heartbeat row; hostedMaps
            // above (this boot's fresh read of admin.ShardMapAssignments) is authoritative for it, not a
            // second, redundant claim built from the same table.
            if (!claimedShardIds.Add(shard.ShardId))
                continue;

            var otherHostedMaps = await shardMapAssignments.GetHostedMapsAsync(shard.ShardId, ct);
            claims.Add(new ShardMapPartitionValidator.ShardMapClaim(shard.ShardId, otherHostedMaps));
        }

        // Liveness-independent complement to the loop above: a shard identifier in admin.ShardMapAssignments
        // that has simply never gone live yet (the simultaneous-cold-boot case) never appears in liveShards,
        // so without this, its claim would never be built and a genuine map overlap with it would pass
        // undetected. One extra read of the whole table, grouped by ShardId; shards already claimed above
        // (this shard itself, or an already-live shard) are skipped rather than double-claimed.
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

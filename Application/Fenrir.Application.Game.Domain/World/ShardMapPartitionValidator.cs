namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Pure ADR-0012 rule 1 check ("a shard is a disjoint map partition of maps, never a replica"): given
///     every currently-live shard's claimed set of map ids, finds any pair of shards that both claim the
///     same map. Two shards hosting the same map is exactly the split-brain the ADR calls out -- the
///     process-wide singletons (party/chat/world state) of each shard would silently diverge for players
///     on that map.
/// </summary>
/// <remarks>
///     No SQL, no I/O: callers assemble <see cref="ShardMapClaim" />s from <c>runtime.GameServerDirectory</c>
///     (who is alive right now, stale heartbeats already excluded) crossed with
///     <c>admin.ShardMapAssignments</c> via <c>IShardMapAssignmentRepository.GetHostedMapsAsync</c> (what
///     each live shard claims to host) -- the same two sources <c>ZoneTransferHandler</c> already reads for
///     login routing (ADR-0012 rule 4). A live shard's claim must come from that shard's own heartbeat-time
///     read, not a fresh re-query of the assignment table for its id: the table can be edited (a map moved
///     to a different shard) while the old shard process keeps running with the previous set of maps
///     already loaded into its <c>ZoneRegistry</c> -- the DB's <c>UNIQUE (MapId)</c> constraint cannot see
///     that in-memory state, which is exactly why this boot-time check exists.
/// </remarks>
public static class ShardMapPartitionValidator
{
    /// <summary>
    ///     Single pass over every claimed map id. Order-independent: a conflicting pair is reported once,
    ///     with <see cref="ShardMapConflict.ShardIdA" /> always the lower shard id, and every overlapping
    ///     map id for that pair collected together.
    /// </summary>
    public static IReadOnlyList<ShardMapConflict> FindConflicts(IReadOnlyList<ShardMapClaim> claims)
    {
        var claimedBy = new Dictionary<short, byte>();
        var overlapsByPair = new Dictionary<(byte Lower, byte Higher), List<short>>();

        foreach (var claim in claims)
        foreach (var mapId in claim.MapIds)
        {
            if (!claimedBy.TryGetValue(mapId, out var firstShardId))
            {
                claimedBy[mapId] = claim.ShardId;
                continue;
            }

            // Same shard listed twice for the same map (e.g. a duplicate directory row) is not a cross-shard conflict.
            if (firstShardId == claim.ShardId)
                continue;

            var pair = firstShardId < claim.ShardId
                ? (firstShardId, claim.ShardId)
                : (claim.ShardId, firstShardId);

            if (!overlapsByPair.TryGetValue(pair, out var mapIds))
                overlapsByPair[pair] = mapIds = [];

            if (!mapIds.Contains(mapId))
                mapIds.Add(mapId);
        }

        return overlapsByPair
            .Select(entry => new ShardMapConflict(entry.Key.Lower, entry.Key.Higher, entry.Value))
            .ToArray();
    }

    /// <summary>One shard's claimed set of map ids, as of whatever the caller considers "now".</summary>
    public readonly record struct ShardMapClaim(byte ShardId, IReadOnlyCollection<short> MapIds);

    /// <summary>Two shards both claiming at least one of the same map ids.</summary>
    public readonly record struct ShardMapConflict(byte ShardIdA, byte ShardIdB, IReadOnlyList<short> MapIds);
}

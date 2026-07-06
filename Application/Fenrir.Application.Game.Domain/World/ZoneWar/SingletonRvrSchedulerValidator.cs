namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Pure check that a designated-map-keyed singleton RvR scheduler (<c>TribeVoteElectionCalendarHost</c>,
///     <c>TribeSymbolBattleSchedulerHost</c>, <c>HolyStoneWarCycleHost</c>) is actually hosted by some live
///     shard. Complements <see cref="ShardMapPartitionValidator" />'s "at most one shard ever claims a map"
///     guarantee (already enforced by ADR-0012's <c>UNIQUE(MapId)</c> constraint plus
///     <c>ShardPartitionGuard</c>) with the other half: "at least one shard claims the map this scheduler
///     needs" -- nothing enforced that before this type existed.
/// </summary>
/// <remarks>
///     No SQL, no I/O: callers assemble <see cref="DesignatedMapClaim" />s from each singleton scheduler's own
///     configured map id and cross them against every currently-live shard's claimed map set (the same two
///     sources <see cref="ShardMapPartitionValidator" /> already crosses) -- see
///     <c>SingletonRvrSchedulerGuard</c> for the thin I/O-crossing wrapper that assembles these.
/// </remarks>
public static class SingletonRvrSchedulerValidator
{
    /// <summary>
    ///     A designated map id of 0 is the documented "not yet configured" sentinel (matching
    ///     <see cref="GameServerOptions.HolyStoneMapId" />'s own convention) and is always skipped -- an
    ///     operator who hasn't configured a system yet gets no boot-time noise about it.
    /// </summary>
    public static IReadOnlyList<UnclaimedDesignatedMap> FindUnclaimed(
        IReadOnlyList<DesignatedMapClaim> designatedMaps, IReadOnlyCollection<short> liveClaimedMapIds)
    {
        var claimed = liveClaimedMapIds as ISet<short> ?? liveClaimedMapIds.ToHashSet();

        return designatedMaps
            .Where(designated => designated.MapId != 0 && !claimed.Contains(designated.MapId))
            .Select(designated => new UnclaimedDesignatedMap(designated.SchedulerName, designated.MapId))
            .ToArray();
    }

    /// <summary>One singleton scheduler's configured designated map id (0 = not configured, always skipped).</summary>
    public readonly record struct DesignatedMapClaim(string SchedulerName, short MapId);

    /// <summary>A designated map that no currently-live shard claims -- its scheduler is inert cluster-wide.</summary>
    public readonly record struct UnclaimedDesignatedMap(string SchedulerName, short MapId);
}

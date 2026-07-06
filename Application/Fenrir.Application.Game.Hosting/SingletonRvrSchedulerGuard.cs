using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Boot-time complement to <see cref="ShardPartitionGuard" />: where that guard enforces "at most one live
///     shard ever claims a map" (ADR-0012 rule 1), this checks the other half -- "at least one live shard
///     claims the map a singleton RvR scheduler is configured to run on". An unclaimed designated map means
///     that scheduler is inert cluster-wide (a service-degradation risk), not that two shards are about to
///     diverge shared world state (<see cref="ShardPartitionGuard" />'s data-corruption risk) -- so, unlike
///     that guard, this one never throws; it only returns what it found for the caller to log.
/// </summary>
public static class SingletonRvrSchedulerGuard
{
    public static async Task<IReadOnlyList<SingletonRvrSchedulerValidator.UnclaimedDesignatedMap>>
        FindUnclaimedDesignatedMapsAsync(
            IReadOnlyList<SingletonRvrSchedulerValidator.DesignatedMapClaim> designatedMaps,
            IGameServerDirectoryRepository directory, IShardMapAssignmentRepository shardMapAssignments,
            CancellationToken ct)
    {
        var liveShards = await directory.GetDirectoryAsync(ct);

        var claimedMapIds = new HashSet<short>();
        foreach (var shard in liveShards)
        foreach (var mapId in await shardMapAssignments.GetHostedMapsAsync(shard.ShardId, ct))
            claimedMapIds.Add(mapId);

        return SingletonRvrSchedulerValidator.FindUnclaimed(designatedMaps, claimedMapIds);
    }
}

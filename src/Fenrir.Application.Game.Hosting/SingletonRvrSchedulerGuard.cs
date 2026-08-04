using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Hosting;

public static class SingletonRvrSchedulerGuard
{
    public static async Task<IReadOnlyList<SingletonRvrSchedulerValidator.UnclaimedDesignatedMap>>
        FindUnclaimedDesignatedMapsAsync(
            IReadOnlyList<SingletonRvrSchedulerValidator.DesignatedMapClaim> designatedMaps,
            IReadOnlyCollection<short> localHostedMapIds,
            IGameServerDirectoryRepository directory, IShardMapAssignmentRepository shardMapAssignments,
            CancellationToken ct)
    {
        var liveShards = await directory.GetDirectoryAsync(ct);

        var claimedMapIds = new HashSet<short>(localHostedMapIds);
        foreach (var shard in liveShards)
        foreach (var mapId in await shardMapAssignments.GetHostedMapsAsync(shard.ShardId, ct))
            claimedMapIds.Add(mapId);

        return SingletonRvrSchedulerValidator.FindUnclaimed(designatedMaps, claimedMapIds);
    }
}

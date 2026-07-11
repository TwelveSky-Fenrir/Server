namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class SingletonRvrSchedulerValidator
{
    public static IReadOnlyList<UnclaimedDesignatedMap> FindUnclaimed(
        IReadOnlyList<DesignatedMapClaim> designatedMaps, IReadOnlyCollection<short> liveClaimedMapIds)
    {
        var claimed = liveClaimedMapIds as ISet<short> ?? liveClaimedMapIds.ToHashSet();

        return designatedMaps
            .Where(designated => designated.MapId != 0 && !claimed.Contains(designated.MapId))
            .Select(designated => new UnclaimedDesignatedMap(designated.SchedulerName, designated.MapId))
            .ToArray();
    }

    public readonly record struct DesignatedMapClaim(string SchedulerName, short MapId);

    public readonly record struct UnclaimedDesignatedMap(string SchedulerName, short MapId);
}

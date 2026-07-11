namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class ValleyWarCenterRelayCodes
{
    public const int RelayRangeStart = 601;

    public const int RelayRangeEnd = 675;

    public const int GodIndex2ClusterStart = 601;

    public const int GodIndex2ClusterEnd = 610;

    public const int MonsterSiegeClusterStart = 611;

    public const int MonsterSiegeClusterEnd = 615;

    public static bool IsInRelayRange(int eventCode)
    {
        return eventCode is >= RelayRangeStart and <= RelayRangeEnd;
    }

    public static bool IsGodIndex2Cluster(int eventCode)
    {
        return eventCode is >= GodIndex2ClusterStart and <= GodIndex2ClusterEnd;
    }

    public static bool IsMonsterSiegeCluster(int eventCode)
    {
        return eventCode is >= MonsterSiegeClusterStart and <= MonsterSiegeClusterEnd;
    }
}

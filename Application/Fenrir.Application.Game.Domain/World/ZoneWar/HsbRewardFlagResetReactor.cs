namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class HsbRewardFlagResetReactor
{

        public static void Apply(ZoneRegistry zones)
    {
        foreach (var zone in zones.Zones)
        foreach (var player in zone.Players)
            player.HsbStoneRewardClaimed = false;
    }
}

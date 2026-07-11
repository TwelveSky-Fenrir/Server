namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class TribeGuardForceResetSweep
{

        public static int Wipe(Zone zone, int poolServerIndexBase)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var wiped = 0;
        for (var relativeIndex = 0; relativeIndex < TribeGuardRegionLayout.RegionSlotCount; relativeIndex++)
        {
            var serverIndex = poolServerIndexBase + relativeIndex;
            if (!zone.TryGetMonster(serverIndex, out _))
                continue;

            zone.DespawnMonsterSilently(serverIndex);
            wiped++;
        }

        return wiped;
    }
}

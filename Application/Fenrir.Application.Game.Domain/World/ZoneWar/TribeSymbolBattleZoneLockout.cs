namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class TribeSymbolBattleZoneLockout
{

        public const short GuardedDestinationZoneId = 38;

        public static readonly IReadOnlySet<short> GuardedSourceZoneIds = new HashSet<short> { 40, 41, 42 };

        public static bool IsLockedOut(short sourceZoneId, short destinationZoneId, bool tribeSymbolBattleActive)
    {
        return tribeSymbolBattleActive &&
               destinationZoneId == GuardedDestinationZoneId &&
               GuardedSourceZoneIds.Contains(sourceZoneId);
    }
}

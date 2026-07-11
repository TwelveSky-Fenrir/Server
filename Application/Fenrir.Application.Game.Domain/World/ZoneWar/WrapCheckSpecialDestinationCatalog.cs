using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class WrapCheckSpecialDestinationCatalog
{
    private static readonly FrozenSet<short> WinZone038Destinations =
        new short[] { 39, 74, 144, 145, 313 }.ToFrozenSet();

    private static readonly FrozenDictionary<short, int> RequiredRebirthCountByDestination =
        new Dictionary<short, int>
        {
            [241] = 1,
            [242] = 2,
            [243] = 3,
            [244] = 4,
            [245] = 5,
            [246] = 6,
            [311] = 6,
            [247] = 7,
            [248] = 8,
            [249] = 9,
            [292] = 10,
            [293] = 11,
            [294] = 12,
            [312] = 12
        }.ToFrozenDictionary();

    private static readonly FrozenSet<short> InstancedDestinations =
        new short[] { 325, 326, 327, 328, 329, 330 }.ToFrozenSet();

        public static bool IsWinZone038Destination(short zoneId)
    {
        return WinZone038Destinations.Contains(zoneId);
    }

        public static bool TryGetRequiredRebirthCount(short zoneId, out int requiredRebirthCount)
    {
        return RequiredRebirthCountByDestination.TryGetValue(zoneId, out requiredRebirthCount);
    }

        public static bool IsInstancedDestination(short zoneId)
    {
        return InstancedDestinations.Contains(zoneId);
    }
}

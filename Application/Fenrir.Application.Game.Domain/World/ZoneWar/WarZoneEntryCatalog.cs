using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public readonly record struct WarZoneEntryRule(
    short ZoneNumber,
    int? MinCombinedLevel,
    int? MaxCombinedLevel,
    int MinRebirthCount,
    int MaxRebirthCount)
{
    public bool HasCombinedLevelRequirement => MinCombinedLevel.HasValue && MaxCombinedLevel.HasValue;
}

public static class WarZoneEntryCatalog
{
    public static readonly FrozenDictionary<short, WarZoneEntryRule> Rules =
        new Dictionary<short, WarZoneEntryRule>
        {
            [164] = new(164, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 0, 6),
            [295] = new(295, null, null, 0, 6),
            [296] = new(296, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 7,
                RebirthProgression.MaxRebirthGeneration),
            [322] = new(322, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 0, 6),
            [323] = new(323, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 7,
                RebirthProgression.MaxRebirthGeneration),
            [335] = new(335, 145, RebirthProgression.CombinedLevelCap, 0, 12)
        }.ToFrozenDictionary();

    public static bool TryGetRule(short zoneNumber, out WarZoneEntryRule rule)
    {
        return Rules.TryGetValue(zoneNumber, out rule);
    }
}

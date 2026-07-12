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
    public const short OdawaCaveMinZoneNumber = 251;
    public const short OdawaCaveMaxZoneNumber = 266;
    public const int OdawaCaveMinCombinedLevel = 146;
    public const int OdawaCaveMaxCombinedLevel = 157;

    public static readonly FrozenDictionary<short, WarZoneEntryRule> Rules = BuildRules();

    public static bool TryGetRule(short zoneNumber, out WarZoneEntryRule rule)
    {
        return Rules.TryGetValue(zoneNumber, out rule);
    }

    private static FrozenDictionary<short, WarZoneEntryRule> BuildRules()
    {
        var rules = new Dictionary<short, WarZoneEntryRule>
        {
            [164] = new(164, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 0, 6),
            [295] = new(295, null, null, 0, 6),
            [296] = new(296, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 7,
                RebirthProgression.MaxRebirthGeneration),
            [322] = new(322, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 0, 6),
            [323] = new(323, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 7,
                RebirthProgression.MaxRebirthGeneration),
            [335] = new(335, 145, RebirthProgression.CombinedLevelCap, 0, 12)
        };

        for (var zoneNumber = OdawaCaveMinZoneNumber; zoneNumber <= OdawaCaveMaxZoneNumber; zoneNumber++)
            rules[zoneNumber] = new WarZoneEntryRule(zoneNumber, OdawaCaveMinCombinedLevel,
                OdawaCaveMaxCombinedLevel, 0, RebirthProgression.MaxRebirthGeneration);

        return rules.ToFrozenDictionary();
    }
}

using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World.Configuration;

namespace Fenrir.Application.Game.Domain.Simulation;

public readonly record struct Zone175InstanceConfig(
    int Index1,
    int Index2,
    int ExperienceRatio,
    int MoneyRatio);

public sealed class Zone175LabyrinthConfig
{
    public static readonly Zone175LabyrinthConfig Disabled = new(FrozenDictionary<short, Zone175InstanceConfig>.Empty);

    private readonly FrozenDictionary<short, Zone175InstanceConfig> _byMapId;

    public Zone175LabyrinthConfig(IReadOnlyDictionary<short, Zone175InstanceConfig> byMapId)
    {
        _byMapId = byMapId as FrozenDictionary<short, Zone175InstanceConfig> ?? byMapId.ToFrozenDictionary();
    }

    public int Count => _byMapId.Count;

    public static Zone175LabyrinthConfig Create(IReadOnlyDictionary<int, ZoneConfig> zoneSettings)
    {
        ArgumentNullException.ThrowIfNull(zoneSettings);

        var maps = new Dictionary<short, (int Index1, int Index2)>
        {
            [175] = (0, 0), [176] = (1, 0), [177] = (2, 0),
            [178] = (0, 1), [179] = (1, 1), [180] = (2, 1), [181] = (3, 1),
            [182] = (0, 2), [183] = (1, 2), [184] = (2, 2), [185] = (3, 2),
            [186] = (0, 3), [187] = (1, 3), [188] = (2, 3), [189] = (3, 3),
            [190] = (0, 4), [191] = (1, 4), [192] = (2, 4), [193] = (3, 4),
            [19] = (0, 5), [25] = (0, 6), [31] = (0, 7),
            [20] = (1, 5), [26] = (1, 6), [32] = (1, 7),
            [21] = (2, 5), [27] = (2, 6), [33] = (2, 7),
            [34] = (3, 5), [35] = (3, 6), [36] = (3, 7)
        };

        var configured = new Dictionary<short, Zone175InstanceConfig>(maps.Count);
        foreach (var (mapId, cell) in maps)
        {
            zoneSettings.TryGetValue(mapId, out var settings);
            configured.Add(mapId, new Zone175InstanceConfig(cell.Index1, cell.Index2,
                settings.SpecialZone175ExperienceRatio, settings.SpecialZone175MoneyRatio));
        }

        return new Zone175LabyrinthConfig(configured);
    }

    public bool TryGet(short mapId, out Zone175InstanceConfig config)
    {
        return _byMapId.TryGetValue(mapId, out config);
    }
}

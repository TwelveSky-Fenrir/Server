using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Simulation;

public readonly record struct Zone175InstanceConfig(
    int Index1,
    int Index2,
    float ExperienceRatio,
    float MoneyRatio);

public sealed class Zone175LabyrinthConfig
{

        public static readonly Zone175LabyrinthConfig Disabled = new(FrozenDictionary<short, Zone175InstanceConfig>.Empty);

    private readonly FrozenDictionary<short, Zone175InstanceConfig> _byMapId;

    public Zone175LabyrinthConfig(IReadOnlyDictionary<short, Zone175InstanceConfig> byMapId)
    {
        _byMapId = byMapId as FrozenDictionary<short, Zone175InstanceConfig> ?? byMapId.ToFrozenDictionary();
    }

        public int Count => _byMapId.Count;

        public bool TryGet(short mapId, out Zone175InstanceConfig config)
    {
        return _byMapId.TryGetValue(mapId, out config);
    }
}

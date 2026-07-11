using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.Configuration;

public sealed class ZoneConfigCatalog
{

        public const int MinValidZoneNumber = 1;

        public const int MaxValidZoneNumber = 350;

        public const int MinMaxUser = 1;

        public const int MaxMaxUser = 1000;

    private readonly FrozenDictionary<int, ZoneConfig> _byMapId;

        public ZoneConfigCatalog(IEnumerable<KeyValuePair<int, ZoneConfig>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var staging = new Dictionary<int, ZoneConfig>();
        foreach (var (mapId, config) in entries)
            staging[mapId] = config;

        _byMapId = staging.ToFrozenDictionary();
    }

        public static ZoneConfigCatalog Empty { get; } = new(Array.Empty<KeyValuePair<int, ZoneConfig>>());

        public int Count => _byMapId.Count;

        public bool TryGet(int mapId, out ZoneConfig config)
    {
        if (_byMapId.TryGetValue(mapId, out config))
            return true;

        config = ZoneConfig.Unconfigured;
        return false;
    }

        public ZoneConfig GetOrDefault(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config : ZoneConfig.Unconfigured;
    }

        public int GetMinLevel(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config.MinLevel : 0;
    }

        public int GetMaxLevel(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config.MaxLevel : 0;
    }

        public int GetOwnerTribe(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config.OwnerTribe : ZoneConfig.NoOwnerTribe;
    }

        public int GetSecondaryClassification(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config.SecondaryClassification : 0;
    }

        public int GetMaxUser(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config.MaxUser : 0;
    }

        public bool IsWithinLevelBand(int mapId, int level)
    {
        if (!_byMapId.TryGetValue(mapId, out var config) || config.MaxLevel == 0)
            return true;

        return level >= config.MinLevel && level <= config.MaxLevel;
    }

        public static bool IsValidZoneNumber(int zoneNumber)
    {
        return zoneNumber is >= MinValidZoneNumber and <= MaxValidZoneNumber;
    }

        public static bool IsValidMaxUser(int maxUser)
    {
        return maxUser is >= MinMaxUser and <= MaxMaxUser;
    }
}

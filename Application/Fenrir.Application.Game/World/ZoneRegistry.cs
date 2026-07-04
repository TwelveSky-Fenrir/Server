using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Simulation;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.World;

/// <summary>
///     The process's fixed set of zone actors -- one <see cref="Zone" /> per hosted map id, built once at
///     startup into a <see cref="FrozenDictionary{TKey,TValue}" /> and never mutated after.
/// </summary>
public sealed class ZoneRegistry
{
    private readonly DirtyTracker<int> _dirtyTracker;
    private readonly MovementRules _movementRules;
    private readonly GameServerOptions _options;
    private readonly QuestCatalog _questCatalog;
    private readonly ImmutableArray<ISimulationSystem> _systems;
    private readonly WorldDataCache _worldData;
    private readonly ILogger<Zone> _zoneLogger;
    private FrozenDictionary<short, Zone> _zones = FrozenDictionary<short, Zone>.Empty;

    public ZoneRegistry(IOptions<GameServerOptions> options, MovementRules movementRules,
        DirtyTracker<int> dirtyTracker, ILogger<Zone> zoneLogger, WorldDataCache worldData,
        IEnumerable<ISimulationSystem> simulationSystems, QuestCatalog? questCatalog = null)
    {
        _options = options.Value;
        _movementRules = movementRules;
        _dirtyTracker = dirtyTracker;
        _zoneLogger = zoneLogger;
        _worldData = worldData;

        // DI registration order is simulation order -- resolved once here since systems are stateless singletons.
        _systems = simulationSystems.ToImmutableArray();

        // Optional: falls back to Zone's own per-zone default so existing test call sites keep compiling.
        _questCatalog = questCatalog ?? new QuestCatalog(worldData);
    }

    /// <summary>Every hosted zone, in no particular order — the tick host launches one loop per entry.</summary>
    public ImmutableArray<Zone> Zones => _zones.Values;

    /// <summary>Throws for a foreign map; use <see cref="TryGet" /> when the caller must degrade gracefully.</summary>
    public Zone this[short mapId] => _zones[mapId];

    /// <summary>Sum of every zone's live player count — the directory heartbeat's CCU figure for this shard.</summary>
    public int TotalPlayerCount
    {
        get
        {
            var total = 0;
            foreach (var zone in _zones.Values)
                total += zone.PlayerCount;
            return total;
        }
    }

    /// <summary>
    ///     Builds one Zone actor per hosted map id. Must run exactly once at boot, before ZoneTickHost starts
    ///     or any handler resolves a map through this registry.
    /// </summary>
    public void Initialize(IReadOnlyCollection<short> maps)
    {
        _zones = maps.ToFrozenDictionary(
            mapId => mapId,
            mapId => new Zone(mapId, _options, _movementRules, _dirtyTracker, _systems, _zoneLogger, _worldData,
                questCatalog: _questCatalog));
    }

    public bool TryGet(short mapId, [NotNullWhen(true)] out Zone? zone)
    {
        return _zones.TryGetValue(mapId, out zone);
    }

    /// <summary>
    ///     Cross-zone player lookup. A character lives in at most one zone, so first hit wins. False for a
    ///     player mid-handoff -- the caller treats that like logged-out: the next flush finds them in the
    ///     target zone.
    /// </summary>
    public bool TryGetPlayer(int characterId, [NotNullWhen(true)] out PlayerRuntimeState? state)
    {
        foreach (var zone in _zones.Values)
            if (zone.TryGetPlayer(characterId, out state) && state is not null)
                return true;

        state = null;
        return false;
    }

    /// <summary>
    ///     Same as <see cref="TryGetPlayer" />, but also returns the hosting <see cref="Zone" /> so callers can
    ///     post a command back onto that player's own zone instead of mutating state directly.
    /// </summary>
    public bool TryGetPlayerAndZone(int characterId, [NotNullWhen(true)] out PlayerRuntimeState? state,
        [NotNullWhen(true)] out Zone? zone)
    {
        foreach (var candidate in _zones.Values)
            if (candidate.TryGetPlayer(characterId, out state) && state is not null)
            {
                zone = candidate;
                return true;
            }

        state = null;
        zone = null;
        return false;
    }

    /// <summary>
    ///     Cross-zone lookup by avatar name (case-insensitive). Only whisper and friend-locate resolve
    ///     process-wide like this; every other social feature (duel/trade/friend/mentor/party) searches the
    ///     asker's own zone only, via <see cref="Zone.Players" /> directly. O(total connected players) -- fine
    ///     for these low-frequency actions, not a per-tick hot path.
    /// </summary>
    public bool TryGetPlayerByName(string name, [NotNullWhen(true)] out PlayerRuntimeState? state)
    {
        foreach (var zone in _zones.Values)
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                state = candidate;
                return true;
            }

        state = null;
        return false;
    }

    /// <summary>Same as <see cref="TryGetPlayerByName" />, but also returns the hosting <see cref="Zone" /> (needed for the reply's MapId).</summary>
    public bool TryGetPlayerAndZoneByName(string name, [NotNullWhen(true)] out PlayerRuntimeState? state,
        [NotNullWhen(true)] out Zone? zone)
    {
        foreach (var candidate in _zones.Values)
        foreach (var player in candidate.Players)
            if (string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                state = player;
                zone = candidate;
                return true;
            }

        state = null;
        zone = null;
        return false;
    }
}

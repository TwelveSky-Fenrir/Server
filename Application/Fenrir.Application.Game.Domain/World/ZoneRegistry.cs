using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     The process's fixed set of zone actors -- one <see cref="Zone" /> per hosted map id, built once at
///     startup into a <see cref="FrozenDictionary{TKey,TValue}" /> and never mutated after.
/// </summary>
public sealed class ZoneRegistry
{
    private readonly DirtyTracker<int> _dirtyTracker;
    private readonly KillCooldownTracker _killCooldownTracker;
    private readonly MovementRules _movementRules;
    private readonly GameServerOptions _options;
    private readonly QuestCatalog _questCatalog;
    private readonly ImmutableArray<ISimulationSystem> _systems;
    private readonly TowerWarState? _towerWar;
    private readonly WorldDataCache _worldData;
    private readonly WorldStateService? _worldState;
    private readonly ILogger<Zone> _zoneLogger;
    private readonly PartyRegistry? _partyRegistry;
    private readonly DuelRegistry? _duelRegistry;
    private readonly HeroRankPointAccumulator? _heroRankPointAccumulator;
    private readonly ICharacterShardLocationRepository? _characterShardLocations;
    private FrozenDictionary<short, Zone> _zones = FrozenDictionary<short, Zone>.Empty;

    public ZoneRegistry(IOptions<GameServerOptions> options, MovementRules movementRules,
        DirtyTracker<int> dirtyTracker, ILogger<Zone> zoneLogger, WorldDataCache worldData,
        IEnumerable<ISimulationSystem> simulationSystems, QuestCatalog? questCatalog = null,
        KillCooldownTracker? killCooldownTracker = null, TowerWarState? towerWar = null,
        WorldStateService? worldState = null, PartyRegistry? partyRegistry = null,
        DuelRegistry? duelRegistry = null, HeroRankPointAccumulator? heroRankPointAccumulator = null,
        ICharacterShardLocationRepository? characterShardLocations = null)
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

        // Shared process-wide across every zone (C05 anti-farm gate) -- a PvP kill farmed across a zone handoff
        // still hits the same tracker instance instead of each zone starting a fresh cooldown clock.
        _killCooldownTracker = killCooldownTracker ?? new KillCooldownTracker();

        // Optional: null only in test call sites that don't exercise tower rewards -- every zone shares the
        // same process-wide TowerWarState singleton the tower-siege lifecycle already depends on.
        _towerWar = towerWar;

        // Optional: null only in test call sites that don't exercise tower/alliance combat gating -- every
        // zone shares the same process-wide WorldStateService singleton the RvR alliance state already
        // depends on (see Zone.Combat.cs's tower-guardian friendly-fire gate).
        _worldState = worldState;

        // Optional: null only in test call sites that don't exercise the stun request's team-stun/duel gates
        // (Zone.Stun.cs) -- every zone shares the same process-wide PartyRegistry/DuelRegistry singleton
        // every other social feature already depends on.
        _partyRegistry = partyRegistry;
        _duelRegistry = duelRegistry;

        // Optional: null only in test call sites that don't exercise the PvP-kill hero-point grant -- every
        // zone shares the same process-wide HeroRankPointAccumulator singleton so a farmed kill in one zone
        // and its later flush aren't split across independent trackers.
        _heroRankPointAccumulator = heroRankPointAccumulator;

        // Optional: null only in test call sites that don't exercise the cross-shard character-location
        // directory -- every zone shares this same process-wide repository so a true disconnect on any
        // hosted map can clean up its own row (see Zone.PlayerLifecycle.cs's HandleLeave).
        _characterShardLocations = characterShardLocations;
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
                questCatalog: _questCatalog, killCooldownTracker: _killCooldownTracker, towerWar: _towerWar,
                worldState: _worldState, partyRegistry: _partyRegistry, duelRegistry: _duelRegistry,
                heroRankPointAccumulator: _heroRankPointAccumulator,
                characterShardLocations: _characterShardLocations));
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

    /// <summary>
    ///     Same as <see cref="TryGetPlayerByName" />, but also returns the hosting <see cref="Zone" /> (needed for the
    ///     reply's MapId).
    /// </summary>
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

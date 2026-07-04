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
///     The process's fixed set of zone actors — one <see cref="Zone" /> per hosted map id (ADR-0012: a shard
///     hosts a DISJOINT partition of maps), built once at startup (<see cref="Initialize" />) into a
///     <see cref="FrozenDictionary{TKey,TValue}" /> and never mutated after. Everything that used to inject
///     the single M1 <c>Zone</c> singleton injects this instead: routing (<c>EnterWorldHandler</c>) resolves
///     <c>character.MapId</c> here, the tick host runs one task per <see cref="Zones" /> entry, and the
///     process-wide readers (write-behind flush, heartbeat CCU) go through
///     <see cref="TryGetPlayer" />/<see cref="TotalPlayerCount" />.
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

        // DI registration order IS simulation order (report 05 §0's deterministic legacy sequence) -- every
        // zone shares the exact same ordered list of systems, resolved once here rather than per-zone, since
        // ISimulationSystem instances are stateless singletons that operate on whichever Zone they're handed.
        _systems = simulationSystems.ToImmutableArray();

        // Server Logic V9 Progression: one process-wide QuestCatalog shared by every zone this registry
        // builds -- optional (falls back to Zone's own per-zone-built default, see Zone's own remarks) so
        // pre-existing test call sites (ZoneRegistryTests) that construct this directly keep compiling.
        _questCatalog = questCatalog ?? new QuestCatalog(worldData);
    }

    /// <summary>Every hosted zone, in no particular order — the tick host launches one loop per entry.</summary>
    public ImmutableArray<Zone> Zones => _zones.Values;

    /// <summary>
    ///     Direct lookup for a map this process is KNOWN to host — throws for a foreign map, so routing paths
    ///     that must degrade gracefully (world-entry for a character persisted on an unhosted map) use
    ///     <see cref="TryGet" /> instead.
    /// </summary>
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
    ///     or any handler resolves a map through this registry -- GameServer's Program.cs calls this right
    ///     after resolving the shard's map list from admin.ShardMapAssignments, the same "explicit async
    ///     warm-up before host.RunAsync" shape WorldDataLoader.InitializeAsync already uses for world.* data.
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
    ///     Cross-zone player lookup for the process-wide readers (write-behind flush). A character lives in AT
    ///     MOST one zone (Enter/Leave/handoff all preserve that, see <see cref="ZoneTransfer" />), so first hit
    ///     wins. False for a player mid-handoff (already left the source, not yet drained by the target) — the
    ///     caller treats that like the logged-out case: skip now, the next flush finds them in the target zone,
    ///     whose Enter re-marks them dirty.
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
    ///     Same scope as <see cref="TryGetPlayer" />, but also returns the hosting <see cref="Zone" /> -- needed by
    ///     callers that must post a <c>ZoneCommand</c>/<c>InventoryZoneCommand</c> back onto that player's OWN zone
    ///     (single-writer invariant) rather than mutate <see cref="PlayerRuntimeState" /> directly.
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
    ///     Cross-zone lookup by avatar NAME (case-insensitive -- character names are unique under SQL
    ///     Server's default case-insensitive collation, <c>UQ_Characters_Name</c>). Phase C/V6 Social:
    ///     the whisper (CZ_SECRET_CHAT_SEND) and friend-locate (CZ_FRIEND_FIND_SEND) channels are the ONLY
    ///     two social features verified to resolve their target process-wide (via ts25playuser, a
    ///     cross-zone directory service) -- every OTHER social ask (duel/trade/friend/mentor/party) uses
    ///     <c>mUTIL.SearchAvatar</c>, which only ever searches the ASKER's OWN zone process
    ///     (<c>Server/ts25zone/S04_MyWork02.cpp:8276</c> et al.) -- so those features resolve their target
    ///     via <see cref="Zone.Players" /> directly, never through this method. O(total connected players);
    ///     acceptable for these two low-frequency, human-paced actions, not a per-tick hot path.
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
    ///     Same scope as <see cref="TryGetPlayerByName" />, but also returns the hosting <see cref="Zone" /> (the whisper
    ///     reply's own <c>ZoneNumber</c> field needs the target's <c>MapId</c>).
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

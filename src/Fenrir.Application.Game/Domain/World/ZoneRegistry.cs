using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.GameData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World;

public sealed class ZoneRegistry
{
    private readonly IAccountSessionRepository? _accountSessions;
    private readonly CharacterPresenceOwnership? _characterPresenceOwnership;
    private readonly ICharacterShardLocationRepository? _characterShardLocations;
    private readonly DirtyTracker<int> _dirtyTracker;
    private readonly DuelRegistry? _duelRegistry;
    private readonly IEventLogQueue? _eventLogQueue;
    private readonly IFourGuildKillPointQueue? _fourGuildKillPointQueue;
    private readonly FriendRegistry? _friendRegistry;
    private readonly HeroRankPointAccumulator? _heroRankPointAccumulator;
    private readonly MovementRules _movementRules;
    private readonly GameServerOptions _options;
    private readonly PartyRegistry? _partyRegistry;
    private readonly Lazy<IPartyResyncRelayQueue>? _partyResyncRelayQueue;
    private readonly Lazy<IPvpKillCooldownClaimQueue>? _pvpKillCooldownClaims;
    private readonly QuestCatalog _questCatalog;
    private readonly RegularWarActiveMapTracker? _regularWarActiveMapTracker;
    private readonly ISessionTicketRepository? _sessionTickets;
    private readonly Lazy<ZoneCenterBroadcastIngestor>? _siegeIngestor;
    private readonly ZoneCenterSiegeState? _siegeState;
    private readonly ImmutableArray<ISimulationSystem> _systems;
    private readonly TowerWarState? _towerWar;
    private readonly TradeRegistry? _tradeRegistry;
    private readonly TribeSymbolCombatModifiers? _tribeSymbolCombatModifiers;
    private readonly WorldDataCache _worldData;
    private readonly WorldStateService? _worldState;
    private readonly Zone051Zone053SiegeState? _zone051Zone053SiegeState;
    private readonly Zone195NokSanState? _zone195NokSanState;
    private readonly ILogger<Zone> _zoneLogger;
    private FrozenDictionary<short, Zone> _zones = FrozenDictionary<short, Zone>.Empty;

    public ZoneRegistry(IOptions<GameServerOptions> options, MovementRules movementRules,
        DirtyTracker<int> dirtyTracker, ILogger<Zone> zoneLogger, WorldDataCache worldData,
        IEnumerable<ISimulationSystem> simulationSystems, QuestCatalog? questCatalog = null,
        TowerWarState? towerWar = null,
        WorldStateService? worldState = null, PartyRegistry? partyRegistry = null,
        DuelRegistry? duelRegistry = null, HeroRankPointAccumulator? heroRankPointAccumulator = null,
        ICharacterShardLocationRepository? characterShardLocations = null,
        RegularWarActiveMapTracker? regularWarActiveMapTracker = null,
        TradeRegistry? tradeRegistry = null,
        FriendRegistry? friendRegistry = null,
        IEventLogQueue? eventLogQueue = null,
        IFourGuildKillPointQueue? fourGuildKillPointQueue = null,
        TribeSymbolCombatModifiers? tribeSymbolCombatModifiers = null,
        Zone195NokSanState? zone195NokSanState = null,
        Lazy<IPartyResyncRelayQueue>? partyResyncRelayQueue = null,
        IAccountSessionRepository? accountSessions = null,
        ISessionTicketRepository? sessionTickets = null,
        CharacterPresenceOwnership? characterPresenceOwnership = null,
        ZoneCenterSiegeState? siegeState = null,
        Zone051Zone053SiegeState? zone051Zone053SiegeState = null,
        Lazy<ZoneCenterBroadcastIngestor>? siegeIngestor = null,
        Lazy<IPvpKillCooldownClaimQueue>? pvpKillCooldownClaims = null)
    {
        _options = options.Value;
        _movementRules = movementRules;
        _dirtyTracker = dirtyTracker;
        _zoneLogger = zoneLogger;
        _worldData = worldData;

        _systems = simulationSystems.ToImmutableArray();

        _questCatalog = questCatalog ?? new QuestCatalog(worldData);

        _towerWar = towerWar;

        _worldState = worldState;

        _partyRegistry = partyRegistry;
        _duelRegistry = duelRegistry;
        _friendRegistry = friendRegistry;

        _tradeRegistry = tradeRegistry;

        _heroRankPointAccumulator = heroRankPointAccumulator;

        _characterShardLocations = characterShardLocations;
        _characterPresenceOwnership = characterPresenceOwnership;

        _regularWarActiveMapTracker = regularWarActiveMapTracker;

        _siegeState = siegeState;

        _eventLogQueue = eventLogQueue;

        _fourGuildKillPointQueue = fourGuildKillPointQueue;

        _tribeSymbolCombatModifiers = tribeSymbolCombatModifiers;

        _zone195NokSanState = zone195NokSanState;

        _zone051Zone053SiegeState = zone051Zone053SiegeState;

        _partyResyncRelayQueue = partyResyncRelayQueue;

        _accountSessions = accountSessions;

        _sessionTickets = sessionTickets;

        _siegeIngestor = siegeIngestor;

        _pvpKillCooldownClaims = pvpKillCooldownClaims;
    }

    public ImmutableArray<Zone> Zones => _zones.Values;

    public Zone this[short mapId] => _zones[mapId];

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

    public void ApplyZone38TribeEffects(Zone38TribeEffectSnapshot snapshot)
    {
        foreach (var zone in _zones.Values)
            if (!zone.Post(ZoneCommand.ApplyZone38TribeEffects(snapshot)))
                _zoneLogger.LogError(
                    "Zone {MapId} rejected the Zone38 tribe-effect snapshot; the actor must be reconciled before combat proceeds",
                    zone.MapId);
    }

    public void Initialize(IReadOnlyCollection<short> maps)
    {
        var sw = Stopwatch.StartNew();
        var canonicalGeometrySources = maps
            .GroupBy(ZoneCanonicalGeometryMap.ResolveCanonicalMapId)
            .Select(static group => group.Key)
            .ToArray();
        var geometries = new ConcurrentDictionary<short, ZoneGeometry>();
        Parallel.ForEach(canonicalGeometrySources,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            canonicalMapId => geometries[canonicalMapId] = Zone.LoadGeometry(canonicalMapId, _options));

        Func<byte, byte>? resolveTribeBankBeneficiary =
            _worldState is null ? null : _worldState.GetTribeSymbolOwner;

        _zones = maps.ToFrozenDictionary(
            mapId => mapId,
            mapId =>
            {
                var geometry = geometries[ZoneCanonicalGeometryMap.ResolveCanonicalMapId(mapId)];
                var zone = new Zone(mapId, _options, _movementRules, _dirtyTracker, _systems, _zoneLogger, _worldData,
                    questCatalog: _questCatalog, towerWar: _towerWar,
                    worldState: _worldState, partyRegistry: _partyRegistry, duelRegistry: _duelRegistry,
                    friendRegistry: _friendRegistry,
                    tradeRegistry: _tradeRegistry,
                    heroRankPointAccumulator: _heroRankPointAccumulator,
                    characterShardLocations: _characterShardLocations,
                    tribeBankTax: new TribeBankTaxAccumulator(resolveTribeBankBeneficiary),
                    regularWarActiveMapTracker: _regularWarActiveMapTracker,
                    zoneRegistry: this,
                    geometry: geometry,
                    eventLogQueue: _eventLogQueue,
                    fourGuildKillPointQueue: _fourGuildKillPointQueue,
                    tribeSymbolCombatModifiers: _tribeSymbolCombatModifiers,
                    zone195NokSanState: _zone195NokSanState,
                    partyResyncRelayQueue: _partyResyncRelayQueue,
                    accountSessions: _accountSessions,
                    sessionTickets: _sessionTickets,
                    characterPresenceOwnership: _characterPresenceOwnership,
                    siegeState: _siegeState,
                    zone051Zone053SiegeState: _zone051Zone053SiegeState,
                    siegeIngestor: _siegeIngestor,
                    pvpKillCooldownClaims: _pvpKillCooldownClaims);

                if (_options.ChallengeContentEnabled)
                    zone.PersonalDungeonBossCatalog = Zone241RebirthTierBossCatalog.Instance;

                return zone;
            });

        _zoneLogger.LogInformation(
            "ZoneRegistry ready: {ZoneCount} zone(s) built; required navmesh (.WM) parsed from {GeometryAssetCount} " +
            "canonical asset(s) for every hosted map in {ElapsedMs} ms (bounded parallel pre-load, off the boot " +
            "critical path)", _zones.Count, canonicalGeometrySources.Length,
            sw.ElapsedMilliseconds);
    }

    public bool TryGet(short mapId, [NotNullWhen(true)] out Zone? zone)
    {
        return _zones.TryGetValue(mapId, out zone);
    }

    public bool TryGetPlayer(int characterId, [NotNullWhen(true)] out PlayerRuntimeState? state)
    {
        foreach (var zone in _zones.Values)
            if (zone.TryGetPlayer(characterId, out state) && state is not null)
                return true;

        state = null;
        return false;
    }

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

    public bool TryGetPlayerInOtherZone(int characterId, Zone excludeZone,
        [NotNullWhen(true)] out PlayerRuntimeState? state,
        [NotNullWhen(true)] out Zone? zone)
    {
        foreach (var candidate in _zones.Values)
        {
            if (ReferenceEquals(candidate, excludeZone))
                continue;

            if (candidate.TryGetPlayer(characterId, out state) && state is not null)
            {
                zone = candidate;
                return true;
            }
        }

        state = null;
        zone = null;
        return false;
    }

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

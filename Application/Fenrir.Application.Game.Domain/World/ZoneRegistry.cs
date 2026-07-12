using System.Collections.Frozen;
using System.Collections.Immutable;
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
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World;

public sealed class ZoneRegistry
{
    private readonly ICharacterShardLocationRepository? _characterShardLocations;
    private readonly DirtyTracker<int> _dirtyTracker;
    private readonly DuelRegistry? _duelRegistry;
    private readonly IEventLogQueue? _eventLogQueue;
    private readonly IFourGuildKillPointQueue? _fourGuildKillPointQueue;
    private readonly FriendRegistry? _friendRegistry;
    private readonly HeroRankPointAccumulator? _heroRankPointAccumulator;
    private readonly KillCooldownTracker _killCooldownTracker;
    private readonly MovementRules _movementRules;
    private readonly GameServerOptions _options;
    private readonly PartyRegistry? _partyRegistry;
    private readonly Lazy<IPartyResyncRelayQueue>? _partyResyncRelayQueue;
    private readonly QuestCatalog _questCatalog;
    private readonly RegularWarActiveMapTracker? _regularWarActiveMapTracker;
    private readonly ImmutableArray<ISimulationSystem> _systems;
    private readonly TowerWarState? _towerWar;
    private readonly TradeRegistry? _tradeRegistry;
    private readonly TribeSymbolCombatModifiers? _tribeSymbolCombatModifiers;
    private readonly WorldDataCache _worldData;
    private readonly WorldStateService? _worldState;
    private readonly Zone195NokSanState? _zone195NokSanState;
    private readonly ILogger<Zone> _zoneLogger;
    private FrozenDictionary<short, Zone> _zones = FrozenDictionary<short, Zone>.Empty;

    public ZoneRegistry(IOptions<GameServerOptions> options, MovementRules movementRules,
        DirtyTracker<int> dirtyTracker, ILogger<Zone> zoneLogger, WorldDataCache worldData,
        IEnumerable<ISimulationSystem> simulationSystems, QuestCatalog? questCatalog = null,
        KillCooldownTracker? killCooldownTracker = null, TowerWarState? towerWar = null,
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
        Lazy<IPartyResyncRelayQueue>? partyResyncRelayQueue = null)
    {
        _options = options.Value;
        _movementRules = movementRules;
        _dirtyTracker = dirtyTracker;
        _zoneLogger = zoneLogger;
        _worldData = worldData;

        _systems = simulationSystems.ToImmutableArray();

        _questCatalog = questCatalog ?? new QuestCatalog(worldData);

        _killCooldownTracker = killCooldownTracker ?? new KillCooldownTracker();

        _towerWar = towerWar;

        _worldState = worldState;

        _partyRegistry = partyRegistry;
        _duelRegistry = duelRegistry;
        _friendRegistry = friendRegistry;

        _tradeRegistry = tradeRegistry;

        _heroRankPointAccumulator = heroRankPointAccumulator;

        _characterShardLocations = characterShardLocations;

        _regularWarActiveMapTracker = regularWarActiveMapTracker;

        _eventLogQueue = eventLogQueue;

        _fourGuildKillPointQueue = fourGuildKillPointQueue;

        _tribeSymbolCombatModifiers = tribeSymbolCombatModifiers;

        _zone195NokSanState = zone195NokSanState;

        _partyResyncRelayQueue = partyResyncRelayQueue;
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

    public void Initialize(IReadOnlyCollection<short> maps)
    {
        _zones = maps.ToFrozenDictionary(
            mapId => mapId,
            mapId =>
            {
                var zone = new Zone(mapId, _options, _movementRules, _dirtyTracker, _systems, _zoneLogger, _worldData,
                    questCatalog: _questCatalog, killCooldownTracker: _killCooldownTracker, towerWar: _towerWar,
                    worldState: _worldState, partyRegistry: _partyRegistry, duelRegistry: _duelRegistry,
                    friendRegistry: _friendRegistry,
                    tradeRegistry: _tradeRegistry,
                    heroRankPointAccumulator: _heroRankPointAccumulator,
                    characterShardLocations: _characterShardLocations,
                    regularWarActiveMapTracker: _regularWarActiveMapTracker,
                    zoneRegistry: this,
                    eventLogQueue: _eventLogQueue,
                    fourGuildKillPointQueue: _fourGuildKillPointQueue,
                    tribeSymbolCombatModifiers: _tribeSymbolCombatModifiers,
                    zone195NokSanState: _zone195NokSanState,
                    partyResyncRelayQueue: _partyResyncRelayQueue);

                zone.PersonalDungeonBossCatalog = Zone241RebirthTierBossCatalog.Instance;

                return zone;
            });
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

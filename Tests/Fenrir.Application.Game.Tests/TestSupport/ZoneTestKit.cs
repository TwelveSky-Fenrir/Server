using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal static class ZoneTestKit
{
    public static GameServerOptions Options()
    {
        return new GameServerOptions();
    }

    public static Zone CreateZone(short mapId, GameServerOptions? options = null,
        DirtyTracker<int>? dirtyTracker = null, IReadOnlyList<ISimulationSystem>? simulationSystems = null,
        WorldDataCache? worldData = null, IRandomSource? randomSource = null,
        KillCooldownTracker? killCooldownTracker = null, TowerWarState? towerWar = null,
        WorldStateService? worldState = null, PartyRegistry? partyRegistry = null,
        DuelRegistry? duelRegistry = null, FriendRegistry? friendRegistry = null,
        ICharacterShardLocationRepository? characterShardLocations = null,
        TribeBankTaxAccumulator? tribeBankTax = null,
        RegularWarActiveMapTracker? regularWarActiveMapTracker = null,
        ZoneGeometry? geometry = null,
        IEventLogQueue? eventLogQueue = null,
        IFourGuildKillPointQueue? fourGuildKillPointQueue = null,
        TribeSymbolCombatModifiers? tribeSymbolCombatModifiers = null)
    {
        var opts = options ?? Options();
        return new Zone(mapId, opts, new MovementRules(Microsoft.Extensions.Options.Options.Create(opts)),
            dirtyTracker ?? new DirtyTracker<int>(), simulationSystems ?? [], NullLogger<Zone>.Instance,
            worldData ?? EmptyWorldData(), randomSource, killCooldownTracker: killCooldownTracker,
            towerWar: towerWar, worldState: worldState, partyRegistry: partyRegistry, duelRegistry: duelRegistry,
            friendRegistry: friendRegistry,
            characterShardLocations: characterShardLocations, tribeBankTax: tribeBankTax,
            regularWarActiveMapTracker: regularWarActiveMapTracker, geometry: geometry,
            eventLogQueue: eventLogQueue, fourGuildKillPointQueue: fourGuildKillPointQueue,
            tribeSymbolCombatModifiers: tribeSymbolCombatModifiers);
    }

    public static (ZoneClientSession Session, FakeDuplexPipe Pipe) CreateSession(long sessionId)
    {
        var pipe = new FakeDuplexPipe();
        return (new ZoneClientSession(sessionId, pipe), pipe);
    }

    public static WorldStateService CreateWorldState(FakeWorldStateRepository? repository = null)
    {
        var service = new WorldStateService(repository ?? new FakeWorldStateRepository(),
            NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    public static ZoneRegistry CreateRegistry(GameServerOptions? options = null, WorldDataCache? worldData = null,
        PartyRegistry? partyRegistry = null, DuelRegistry? duelRegistry = null)
    {
        var opts = options ?? Options();
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(opts);
        return new ZoneRegistry(optionsWrapper, new MovementRules(optionsWrapper), new DirtyTracker<int>(),
            NullLogger<Zone>.Instance, worldData ?? EmptyWorldData(), [], partyRegistry: partyRegistry,
            duelRegistry: duelRegistry);
    }

    public static PlayerEnterData EnterData(ZoneClientSession session, short mapId, string name = "Hero",
        float posX = 100f, float posY = 0f, float posZ = 100f, long flushSequence = 7, byte tribe = 1,
        short level = 42, string? sourceIp = null)
    {
        return new PlayerEnterData(
            session,
            name,
            tribe,
            0,
            2,
            3,
            level,
            mapId,
            posX,
            posY,
            posZ,
            1.5f,
            800,
            840,
            300,
            320,
            flushSequence,
            SourceIp: sourceIp);
    }

    public static byte[] DrainOutbound(FakeDuplexPipe pipe)
    {
        if (!pipe.SessionToPeer.TryRead(out var result))
            return [];

        var bytes = result.Buffer.ToArray();
        pipe.SessionToPeer.AdvanceTo(result.Buffer.End);
        return bytes;
    }

    public static WorldDataCache EmptyWorldData(
        FrozenDictionary<int, ItemDefinition>? itemsById = null,
        FrozenDictionary<int, SkillDefinition>? skillsById = null,
        FrozenDictionary<short, LevelRowDto>? levelsByLevel = null,
        FrozenDictionary<short, ZoneDefinition>? zonesByNumber = null,
        FrozenDictionary<int, MonsterDefinition>? monstersById = null)
    {
        return new WorldDataCache
        {
            ItemsById = itemsById ?? EmptyFrozen<int, ItemDefinition>(),
            SkillsById = skillsById ?? EmptyFrozen<int, SkillDefinition>(),
            MonstersById = monstersById ?? EmptyFrozen<int, MonsterDefinition>(),
            NpcsById = EmptyFrozen<int, NpcDefinition>(),
            QuestsById = EmptyFrozen<int, QuestDefinition>(),
            LevelsByLevel = levelsByLevel ?? EmptyFrozen<short, LevelRowDto>(),
            ZonesByNumber = zonesByNumber ?? EmptyFrozen<short, ZoneDefinition>(),
            GemSocketsById = EmptyFrozen<int, GemSocketRowDto>(),
            GemSocketsByTypeAndValue = EmptyFrozen<int, GemSocketRowDto>(),
            BloodExchangeCatalog = [],
            EventDefinitions = [],
            ItemMallProductsById = EmptyFrozen<int, ItemMallProductRowDto>(),
            RewardBundleItemsByBundleId = EmptyFrozen<int, ImmutableArray<RewardBundleItemRowDto>>(),
            CashCatalog = CashCatalogBuilder.Build([]),
            CashCatalogVersion = 0
        };
    }

    private static FrozenDictionary<TKey, TValue> EmptyFrozen<TKey, TValue>() where TKey : notnull
    {
        return new Dictionary<TKey, TValue>().ToFrozenDictionary();
    }
}

internal sealed class ScriptedRandomSource(params int[] sequence) : IRandomSource
{
    private int _index;

    public int NextInt32(int exclusiveUpperBound)
    {
        var value = sequence[_index % sequence.Length] % exclusiveUpperBound;
        _index++;
        return value;
    }
}

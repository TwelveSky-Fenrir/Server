using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Monsters;
using Fenrir.Application.Game.World.WorldState;
using Fenrir.Application.Game.World.ZoneWar;
using Fenrir.Data.World;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers <see cref="MonsterSpawnScheduler.ProcessDeath" />'s "Holy Stone" tribe/monster-symbol report
///     (A013's <c>mSpecialSortNumber==2</c> branch, <c>S07_MyGame05.cpp:1565-1610</c>): killing one of the
///     SpecialType-11/12/13/28/14 guardian monsters must resolve the symbol via
///     <see cref="ZoneEventBroadcaster.AnnounceSymbolResolved" />, using the killer's own tribe (the documented
///     simplification of legacy's cumulative per-tribe damage tally).
/// </summary>
public class MonsterSpawnSchedulerTribeSymbolTests
{
    private static WorldDataCache CacheWithHolyStone(int monsterId, byte specialType)
    {
        var monster = WorldDataTestRows.Monster(monsterId) with
        {
            SpecialType = specialType,
            Life = 100,
            ItemLevel = 1,
            RealLevel = 1,
            GeneralExperience = 0,
            SummonTime1 = 9999,
            SummonTime2 = 9999
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, monsterId) with
        {
            Number = 1,
            LocationX = 0,
            LocationY = 0,
            LocationZ = 0,
            Radius = 0
        };

        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static (WorldStateService WorldState, ZoneEventBroadcaster Broadcaster) CreateWorldStateAndBroadcaster()
    {
        var worldState = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

        // Decoupled from the zone under test -- AnnounceSymbolResolved only needs *a* ZoneRegistry to iterate
        // for the broadcast side; the assertions below only care that WorldStateService itself got mutated.
        var options = ZoneTestKit.Options();
        var emptyRegistry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(), []);
        emptyRegistry.Initialize([]);

        var broadcaster = new ZoneEventBroadcaster(worldState, emptyRegistry, NullLogger<ZoneEventBroadcaster>.Instance);
        return (worldState, broadcaster);
    }

    [Theory]
    [InlineData(601, (byte)11, (byte)0)] // one tribe's own slot
    [InlineData(603, (byte)13, (byte)2)]
    [InlineData(605, (byte)28, (byte)3)]
    public void KillingATribeSlotGuardian_ResolvesThatTribesOwnSlot_ToTheKillersTribe(int monsterId,
        byte specialType, byte expectedSymbolIndex)
    {
        var cache = CacheWithHolyStone(monsterId, specialType);
        var (worldState, broadcaster) = CreateWorldStateAndBroadcaster();
        var scheduler = new MonsterSpawnScheduler(cache, zoneEventBroadcaster: new Lazy<ZoneEventBroadcaster>(broadcaster));
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        var (session, _) = ZoneTestKit.CreateSession(1);
        const byte killerTribe = 2;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Killer", tribe: killerTribe)));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        zone.TryDamageMonster(1, 10_000, 10, out var died, out _);
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick); // drains the death -> ProcessDeath -> AnnounceSymbolResolved

        Assert.Equal(killerTribe == expectedSymbolIndex, worldState.GetTribe(expectedSymbolIndex).HasSymbol);
    }

    [Fact]
    public void KillingTheNeutralMonsterGuardedGuardian_ResolvesTheMonsterSymbol_ToTheKillersTribe()
    {
        var cache = CacheWithHolyStone(604, 14);
        var (worldState, broadcaster) = CreateWorldStateAndBroadcaster();
        var scheduler = new MonsterSpawnScheduler(cache, zoneEventBroadcaster: new Lazy<ZoneEventBroadcaster>(broadcaster));
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        var (session, _) = ZoneTestKit.CreateSession(1);
        const byte killerTribe = 3;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Killer", tribe: killerTribe)));
        zone.Tick(SimulationClock.LegacyTick);

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal((byte?)killerTribe, worldState.World.MonsterSymbol);
    }

    [Fact]
    public void KillingAnOrdinaryMonster_NeverResolvesAnySymbol()
    {
        var cache = CacheWithHolyStone(700, 0); // SpecialType 0 -- not a Holy Stone
        var (worldState, broadcaster) = CreateWorldStateAndBroadcaster();
        var scheduler = new MonsterSpawnScheduler(cache, zoneEventBroadcaster: new Lazy<ZoneEventBroadcaster>(broadcaster));
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Killer", tribe: 1)));
        zone.Tick(SimulationClock.LegacyTick);

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Null(worldState.World.MonsterSymbol);
        Assert.All(worldState.GetAllTribes(), t => Assert.True(t.HasSymbol)); // untouched EnsureInitialized default
    }

    [Fact]
    public void NoZoneEventBroadcasterWired_NeverThrows()
    {
        // The scheduler must remain usable when zoneEventBroadcaster is left null (matches production DI's
        // "optional dependency, auto-resolved if registered" posture) -- this is the whole point of it being
        // an optional constructor parameter.
        var cache = CacheWithHolyStone(601, 11);
        var scheduler = new MonsterSpawnScheduler(cache);
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Killer")));
        zone.Tick(SimulationClock.LegacyTick);

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick); // must not throw
    }
}

using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ZoneValleyWarBossSummonTests
{
    private const short ValleyMapId = 200;

    private static WorldDataCache CacheWithBoss756()
    {
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [WorldDataTestRows.Monster(Zone200GateBreachBossCatalog.BossMonsterId)]
        };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static (ValleyWarSystem System, ValleyWarKillRegistry KillRegistry, ZoneRegistry Registry) CreateSystem(
        WorldDataCache worldData, ILogger<ValleyWarSystem>? logger = null)
    {
        var registry = ZoneTestKit.CreateRegistry(worldData: worldData);
        registry.Initialize([ValleyMapId]);

        var worldState = ZoneTestKit.CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var killRegistry = new ValleyWarKillRegistry();
        var system = new ValleyWarSystem(killRegistry, new Lazy<ZoneEventBroadcaster>(() => broadcaster),
            new Lazy<ZoneRegistry>(() => registry), logger ?? NullLogger<ValleyWarSystem>.Instance);

        return (system, killRegistry, registry);
    }

        private static void EnterPlayer(ZoneRegistry registry, int characterId, byte tribe)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        registry[ValleyMapId].Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, ValleyMapId, tribe: tribe)));
        registry[ValleyMapId].Tick(TimeSpan.FromMilliseconds(50));
    }

        private static void AdvanceToKillRaceStart(ValleyWarSystem system, ValleyWarKillRegistry killRegistry, Zone zone)
    {
        system.Simulate(zone,
            ValleyWarSchedule.IdleWaitTicks +
            (ValleyWarSchedule.GateCountdownStartValue + 1) * ValleyWarSchedule.GateCountdownIntervalTicks +
            ValleyWarSchedule.GateOpenTicks +
            ValleyWarSchedule.DoorPendingTicks);

        Assert.Equal(ValleyWarPhase.KillRace, killRegistry.GetOrCreate(zone.MapId).Phase);
    }

    [Fact]
    public void TribeWin_SummonsExactlyOneBoss756_AtTheCatalogFixedPosition()
    {
        var (system, killRegistry, registry) = CreateSystem(CacheWithBoss756());
        var zone = registry[ValleyMapId];
        EnterPlayer(registry, 1, tribe: 0);

        AdvanceToKillRaceStart(system, killRegistry, zone);
        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(0);
        system.Simulate(zone, 1);

        var monster = Assert.Single(zone.MonstersSnapshot);
        Assert.Equal(Zone200GateBreachBossCatalog.BossMonsterId, monster.Template.MonsterId);
        Assert.Equal(Zone200GateBreachBossCatalog.SummonX, monster.PosX);
        Assert.Equal(Zone200GateBreachBossCatalog.SummonY, monster.PosY);
        Assert.Equal(Zone200GateBreachBossCatalog.SummonZ, monster.PosZ);
    }

    [Fact]
    public void MonsterCatalogMissingBoss756_IsANoOp_NeverThrows()
    {
        var (system, killRegistry, registry) = CreateSystem(ZoneTestKit.EmptyWorldData());
        var zone = registry[ValleyMapId];
        EnterPlayer(registry, 1, tribe: 0);

        AdvanceToKillRaceStart(system, killRegistry, zone);
        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(0);
        system.Simulate(zone, 1);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void SecondTribeWin_WhileFirstBossStillAlive_IsANoOp_ExistenceCheck()
    {
        var (system, killRegistry, registry) = CreateSystem(CacheWithBoss756());
        var zone = registry[ValleyMapId];
        EnterPlayer(registry, 1, tribe: 0);

        AdvanceToKillRaceStart(system, killRegistry, zone);
        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(0);
        system.Simulate(zone, 1);
        Assert.Equal(1, zone.MonsterCount);

        system.Simulate(zone, ValleyWarSchedule.ScrollDeleteDelayTicks);
        system.Simulate(zone, 1);
        system.Simulate(zone, ValleyWarSchedule.PostWinCooldownTicks);
        system.Simulate(zone, ValleyWarSchedule.PreResetTicks);

        Assert.Equal(ValleyWarPhase.Idle, killRegistry.GetOrCreate(ValleyMapId).Phase);
        Assert.Equal(1, zone.MonsterCount);

        AdvanceToKillRaceStart(system, killRegistry, zone);
        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(0);
        system.Simulate(zone, 1);

        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void DoorOpened_GeneralKillRaceMonsterPopulationGap_StillLogsUnchanged()
    {
        var logger = new CapturingLogger<ValleyWarSystem>();
        var (system, killRegistry, registry) = CreateSystem(CacheWithBoss756(), logger);
        var zone = registry[ValleyMapId];
        EnterPlayer(registry, 1, tribe: 0);

        AdvanceToKillRaceStart(system, killRegistry, zone);

        Assert.Contains(logger.Entries,
            e => e.Message.Contains("the general kill-race monster population", StringComparison.Ordinal));
    }

    [Fact]
    public void BossWin_RewardGrant_StillOmitsAnimalFiveSlot_ExactlySevenItemsGranted()
    {
        var (system, killRegistry, registry) = CreateSystem(CacheWithBoss756());
        var zone = registry[ValleyMapId];
        EnterPlayer(registry, 1, tribe: 0);

        AdvanceToKillRaceStart(system, killRegistry, zone);
        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(0);
        system.Simulate(zone, 1);
        system.Simulate(zone, ValleyWarSchedule.ScrollDeleteDelayTicks);
        system.Simulate(zone, 1);

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(7, zone.GroundItemCount);
    }
}

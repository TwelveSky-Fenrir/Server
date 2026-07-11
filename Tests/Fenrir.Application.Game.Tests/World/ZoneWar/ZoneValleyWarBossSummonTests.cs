using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers the closure of the previously-logged Boss-756 (Zone200 gate-breach/kill-race) summon gap:
///     <c>ValleyWarSystem.React</c>'s <c>TribeWin</c> branch now calls <c>Zone.HandleSummonValleyWarBoss</c>
///     directly instead of <c>LogMonsterSummonGap</c>. Distinct from the sibling Boss-561/Regular-War family
///     covered by <see cref="ZoneRegularWarBossSummonTests" /> -- see that class's own remarks, and
///     <c>Zone.ValleyWarBossSummon.cs</c>'s own remarks, for the disambiguation. This file only exercises
///     the summon side effect itself; the broadcast/state-machine behavior around it is already covered by
///     <see cref="ValleyWarSystemTests" />, left entirely unchanged by this fix.
/// </summary>
public class ZoneValleyWarBossSummonTests
{
    private const short ValleyMapId = 200; // one of ValleyWarMapCatalog's 4 configured maps

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

    /// <summary>A single ready, non-mid-transfer avatar -- the KillRace phase's own "eligible player present" gate.</summary>
    private static void EnterPlayer(ZoneRegistry registry, int characterId, byte tribe)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        registry[ValleyMapId].Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, ValleyMapId, tribe: tribe)));
        registry[ValleyMapId].Tick(TimeSpan.FromMilliseconds(50));
    }

    /// <summary>Ticks Idle -&gt; GateCountdown -&gt; GateOpen -&gt; DoorPending, landing exactly on KillRace's first tick.</summary>
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
        system.Simulate(zone, 1); // -> ScrollPending; TribeWin fires here, summon happens inline

        var monster = Assert.Single(zone.MonstersSnapshot);
        Assert.Equal(Zone200GateBreachBossCatalog.BossMonsterId, monster.Template.MonsterId);
        Assert.Equal(Zone200GateBreachBossCatalog.SummonX, monster.PosX);
        Assert.Equal(Zone200GateBreachBossCatalog.SummonY, monster.PosY);
        Assert.Equal(Zone200GateBreachBossCatalog.SummonZ, monster.PosZ);
    }

    [Fact]
    public void MonsterCatalogMissingBoss756_IsANoOp_NeverThrows()
    {
        var (system, killRegistry, registry) = CreateSystem(ZoneTestKit.EmptyWorldData()); // no monster 756 defined
        var zone = registry[ValleyMapId];
        EnterPlayer(registry, 1, tribe: 0);

        AdvanceToKillRaceStart(system, killRegistry, zone);
        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(0);
        system.Simulate(zone, 1); // must not throw

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void SecondTribeWin_WhileFirstBossStillAlive_IsANoOp_ExistenceCheck()
    {
        // Zone200GateBreachBossCatalog.ExistenceCheckActive: unlike Boss 561's summon (which skips this check
        // entirely, see RegularWarBossSummonCatalog.SkipExistenceCheck), a second win must NOT stack a second
        // copy while the first is still alive -- nothing in this test ever kills it.
        var (system, killRegistry, registry) = CreateSystem(CacheWithBoss756());
        var zone = registry[ValleyMapId];
        EnterPlayer(registry, 1, tribe: 0);

        // First full cycle: Idle -> ... -> KillRace -> TribeWin (summons the only copy) -> ... -> back to Idle.
        AdvanceToKillRaceStart(system, killRegistry, zone);
        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(0);
        system.Simulate(zone, 1); // -> ScrollPending; TribeWin -> boss-756 summoned
        Assert.Equal(1, zone.MonsterCount);

        system.Simulate(zone, ValleyWarSchedule.ScrollDeleteDelayTicks); // -> BossWindow
        system.Simulate(zone, 1); // BossSlotOccupied always false -> BossWin -> PostWinCooldown
        system.Simulate(zone, ValleyWarSchedule.PostWinCooldownTicks); // -> PreReset
        system.Simulate(zone, ValleyWarSchedule.PreResetTicks); // -> forced disconnect + reset to Idle

        Assert.Equal(ValleyWarPhase.Idle, killRegistry.GetOrCreate(ValleyMapId).Phase);
        Assert.Equal(1, zone.MonsterCount); // the first copy is still alive -- nothing in this test ever killed it

        // Second full cycle, same schedule instance, same zone, boss from the first win never died.
        AdvanceToKillRaceStart(system, killRegistry, zone);
        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(0);
        system.Simulate(zone, 1); // TribeWin fires again -> HandleSummonValleyWarBoss called again

        Assert.Equal(1, zone.MonsterCount); // existence check: still exactly one copy, no duplicate summoned
    }

    [Fact]
    public void DoorOpened_GeneralKillRaceMonsterPopulationGap_StillLogsUnchanged()
    {
        // Regression guard: this fix touches ONLY the boss-756 LogMonsterSummonGap call on the TribeWin branch.
        // The unrelated, still-genuinely-unresolved general kill-race population gap (logged on DoorOpened) must
        // keep firing exactly as before.
        var logger = new CapturingLogger<ValleyWarSystem>();
        var (system, killRegistry, registry) = CreateSystem(CacheWithBoss756(), logger);
        var zone = registry[ValleyMapId];
        EnterPlayer(registry, 1, tribe: 0);

        AdvanceToKillRaceStart(system, killRegistry, zone); // DoorPending's last tick -> DoorOpened fires mid-call

        Assert.Contains(logger.Entries,
            e => e.Message.Contains("the general kill-race monster population", StringComparison.Ordinal));
    }

    [Fact]
    public void BossWin_RewardGrant_StillOmitsAnimalFiveSlot_ExactlySevenItemsGranted()
    {
        // Regression guard: the OTHER documented gap this class's own remarks still flag (contract §8b's 8th
        // "animal5" reward slot) is untouched by this fix -- exactly 7 of 8 items are still granted, same as
        // before, now alongside a real (not merely logged) boss-756 summon.
        var (system, killRegistry, registry) = CreateSystem(CacheWithBoss756());
        var zone = registry[ValleyMapId];
        EnterPlayer(registry, 1, tribe: 0);

        AdvanceToKillRaceStart(system, killRegistry, zone);
        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(0);
        system.Simulate(zone, 1); // -> ScrollPending; TribeWin -> boss-756 summoned
        system.Simulate(zone, ValleyWarSchedule.ScrollDeleteDelayTicks); // -> BossWindow
        system.Simulate(zone, 1); // BossSlotOccupied always false -> BossWin -> posts the reward-drop command

        zone.Tick(TimeSpan.FromMilliseconds(50)); // drain the war map's own posted GrantValleyWarRewardDrop command

        Assert.Equal(7, zone.GroundItemCount);
    }
}

using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Progression;

/// <summary>
///     A11 -- the tick-side construction lifecycle and tower-attack AI idle timers. <c>Simulate</c> is public, so
///     each phase is driven directly (no dependence on <see cref="Zone.Tick" />'s accumulator) while the wall-clock
///     transitions are pre-aged via <see cref="TowerWarState.CompleteConstructionSpawn" />/<c>RecordGuardianHit</c>
///     with a past timestamp, since the system itself reads <c>DateTime.UtcNow</c>. Zone 2 -&gt; tower index 0;
///     the level-1 Silver guardian is world.Monsters#589.
/// </summary>
public class TowerLifecycleSystemTests
{
    private const short TowerZoneNumber = 2;
    private const int TowerIndex = 0;
    private const int Level1GuardianMonsterId = 589;

    private static WorldDataCache CacheWithGuardian()
    {
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [WorldDataTestRows.Monster(Level1GuardianMonsterId) with { Life = 5000 }]
        };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static (Zone Zone, TowerWarState TowerWar, TowerLifecycleSystem System) Create(
        short mapId = TowerZoneNumber)
    {
        var worldData = CacheWithGuardian();
        var towerWar = new TowerWarState();
        var system = new TowerLifecycleSystem(towerWar, worldData);
        var zone = ZoneTestKit.CreateZone(mapId, worldData: worldData, towerWar: towerWar);
        return (zone, towerWar, system);
    }

    [Fact]
    public void ConstructionArmed_Simulate_SpawnsTheLevel1Guardian_AndStartsTheCreateCooldown()
    {
        var (zone, towerWar, system) = Create();
        Assert.True(towerWar.BeginConstruction(TowerIndex, 1, 0));

        system.Simulate(zone, 1);

        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var guardian));
        Assert.Equal(Level1GuardianMonsterId, guardian!.Template.MonsterId);
        Assert.Equal(-1276f, guardian.PosX);
        Assert.Equal(201, towerWar.GetPackedState(TowerIndex)); // level-1 Silver (200 + kind 1)
        Assert.False(towerWar.IsValid(TowerIndex)); // still cooling, not attackable yet
        Assert.Equal(TowerSiegePhase.Dormant, towerWar.GetPhase(TowerIndex));
        Assert.Equal(0, towerWar.GetPendingConstructKind(TowerIndex)); // cooldown running, kind now in packed state
    }

    [Fact]
    public void NonTowerZone_Simulate_NeverSpawnsAndLeavesTheConstructionUntouched()
    {
        var (zone, towerWar, system) = Create(999); // zone 999 hosts no tower slot
        Assert.True(towerWar.BeginConstruction(TowerIndex, 1, 0));

        system.Simulate(zone, 1);

        Assert.Equal(0, zone.MonsterCount);
        Assert.Equal(1, towerWar.GetPendingConstructKind(TowerIndex)); // this zone is not tower 0's host
    }

    [Fact]
    public void CreateCooldownElapsed_Simulate_PromotesTheTowerToActive()
    {
        var (zone, towerWar, system) = Create();
        towerWar.BeginConstruction(TowerIndex, 1, 0);
        // Guardian already spawned and cooled past its 5-minute create window.
        towerWar.CompleteConstructionSpawn(TowerIndex,
            DateTime.UtcNow - TowerWarState.CreateCooldown - TimeSpan.FromMinutes(1));
        Assert.False(towerWar.IsValid(TowerIndex));

        system.Simulate(zone, 1);

        Assert.True(towerWar.IsValid(TowerIndex));
        Assert.Equal(TowerSiegePhase.Active, towerWar.GetPhase(TowerIndex));
    }

    [Fact]
    public void Simulate_RunsTheAttackAiIdleReset_ReturningTheAttackStateToReady()
    {
        var (zone, towerWar, system) = Create();
        towerWar.RecordGuardianHit(TowerIndex,
            DateTime.UtcNow - TowerWarState.AttackStateIdleReset - TimeSpan.FromSeconds(1));
        Assert.False(towerWar.IsUnderAttack(TowerIndex));

        system.Simulate(zone, 1);

        Assert.True(towerWar.IsUnderAttack(TowerIndex));
    }

    [Fact]
    public void Simulate_RunsTheEngagementAutoClear_ClearingStaleFirstHitTracking()
    {
        var (zone, towerWar, system) = Create();
        towerWar.RecordGuardianHit(TowerIndex,
            DateTime.UtcNow - TowerWarState.EngagementAutoClear - TimeSpan.FromSeconds(1));
        Assert.NotNull(towerWar.GetFirstAttackAtUtc(TowerIndex));

        system.Simulate(zone, 1);

        Assert.Null(towerWar.GetFirstAttackAtUtc(TowerIndex));
    }
}

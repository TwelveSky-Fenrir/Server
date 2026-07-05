using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerGuardianSystemTests
{
    // Zone 2 -> TowerZoneIndexTable towerIndex 0; guardian for level 2/type 1 is world.Monsters#589 (Silver Tower[L1]).
    private const short TowerZoneNumber = 2;
    private const int TowerIndex = 0;
    private const int GuardianMonsterId = 589;
    private const int UpgradedGuardianMonsterId = 590; // level 4/type 1

    private static WorldDataCache CacheWithGuardians()
    {
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters =
            [
                WorldDataTestRows.Monster(GuardianMonsterId) with { Life = 5000 },
                WorldDataTestRows.Monster(UpgradedGuardianMonsterId) with { Life = 9000 }
            ]
        };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static (Zone Zone, TowerWarState TowerWar) CreateZone(WorldDataCache? cache = null)
    {
        var worldData = cache ?? CacheWithGuardians();
        var towerWar = new TowerWarState();
        var system = new TowerGuardianSystem(towerWar, worldData);
        var zone = ZoneTestKit.CreateZone(TowerZoneNumber, simulationSystems: [system], worldData: worldData);
        return (zone, towerWar);
    }

    [Fact]
    public void ZoneWithNoTower_NeverTouchesTheGuardianRegardlessOfPhase()
    {
        var worldData = CacheWithGuardians();
        var towerWar = new TowerWarState();
        var system = new TowerGuardianSystem(towerWar, worldData);
        var zone = ZoneTestKit.CreateZone(999, simulationSystems: [system], worldData: worldData);
        towerWar.SetTowerState(0, 201, true);
        towerWar.BeginUpgrade(0, 401, controllingTribeId: 1);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);
        Assert.Equal(TowerSiegePhase.Building, towerWar.GetPhase(0)); // untouched -- not this zone's tower
    }

    [Fact]
    public void Building_SpawnsTheGuardianAtTheCatalogLocation_AndCompletesTheUpgrade()
    {
        var (zone, towerWar) = CreateZone();
        towerWar.BeginUpgrade(TowerIndex, newPackedState: 201, controllingTribeId: 1);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(1, zone.MonsterCount);
        var guardianIndex = TowerWarState.GuardianServerIndex(TowerIndex);
        Assert.True(zone.TryGetMonster(guardianIndex, out var guardian));
        Assert.Equal(GuardianMonsterId, guardian!.Template.MonsterId);
        Assert.Equal(-1276f, guardian.PosX);
        Assert.Equal(-5f, guardian.PosY);
        Assert.Equal(1826f, guardian.PosZ);

        Assert.Equal(TowerSiegePhase.Active, towerWar.GetPhase(TowerIndex));
        Assert.Equal(201, towerWar.GetPackedState(TowerIndex));
        Assert.Equal((byte?)1, towerWar.GetControllingTribe(TowerIndex));
    }

    [Fact]
    public void Building_WhileAnOlderGuardianIsStillAlive_ReplacesItInsteadOfStacking()
    {
        var (zone, towerWar) = CreateZone();
        towerWar.BeginUpgrade(TowerIndex, 201, controllingTribeId: 1);
        zone.Tick(SimulationClock.LegacyTick); // level 2 guardian spawns

        towerWar.BeginUpgrade(TowerIndex, 401, controllingTribeId: 1); // level-up to 4
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(1, zone.MonsterCount); // old guardian freed, not stacked
        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var guardian));
        Assert.Equal(UpgradedGuardianMonsterId, guardian!.Template.MonsterId);
        Assert.Equal(401, towerWar.GetPackedState(TowerIndex));
    }

    [Fact]
    public void Active_GuardianStillAlive_StaysActive()
    {
        var (zone, towerWar) = CreateZone();
        towerWar.BeginUpgrade(TowerIndex, 201, controllingTribeId: 1);
        zone.Tick(SimulationClock.LegacyTick);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(TowerSiegePhase.Active, towerWar.GetPhase(TowerIndex));
        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void Active_GuardianKilled_BeginsSiege()
    {
        var (zone, towerWar) = CreateZone();
        towerWar.BeginUpgrade(TowerIndex, 201, controllingTribeId: 1);
        zone.Tick(SimulationClock.LegacyTick);
        var guardianIndex = TowerWarState.GuardianServerIndex(TowerIndex);
        zone.TryDamageMonster(guardianIndex, 999_999, attackerCharacterId: null, out var died, out _);
        Assert.True(died);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(TowerSiegePhase.Sieged, towerWar.GetPhase(TowerIndex));
        Assert.False(towerWar.IsValid(TowerIndex));
        Assert.Equal(201, towerWar.GetPackedState(TowerIndex)); // last known level still shown until the destroy cooldown elapses
    }

    [Fact]
    public void Sieged_StaysSieged_BeforeTheCooldownElapses()
    {
        var (zone, towerWar) = CreateZone();
        towerWar.SetTowerState(TowerIndex, 201, true);
        towerWar.BeginSiege(TowerIndex, DateTime.UtcNow - TowerWarState.SiegeCollapseCooldown + TimeSpan.FromSeconds(30));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(TowerSiegePhase.Sieged, towerWar.GetPhase(TowerIndex));
        Assert.Equal(201, towerWar.GetPackedState(TowerIndex));
    }

    [Fact]
    public void Sieged_CollapsesToDormant_OnceTheCooldownHasElapsed()
    {
        var (zone, towerWar) = CreateZone();
        towerWar.SetTowerState(TowerIndex, 201, true);
        towerWar.BeginSiege(TowerIndex, DateTime.UtcNow - TowerWarState.SiegeCollapseCooldown - TimeSpan.FromSeconds(1));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(TowerSiegePhase.Dormant, towerWar.GetPhase(TowerIndex));
        Assert.Equal(0, towerWar.GetPackedState(TowerIndex));
        Assert.Null(towerWar.GetControllingTribe(TowerIndex));
    }

    [Fact]
    public void Dormant_NeverSpawnsAGuardian()
    {
        var (zone, towerWar) = CreateZone();

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);
        Assert.Equal(TowerSiegePhase.Dormant, towerWar.GetPhase(TowerIndex));
    }
}

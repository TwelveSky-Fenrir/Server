using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterSpawnSchedulerTests
{
    private static WorldDataCache CacheWithOneRegion(int number = 1, int summonTimeSeconds = 2,
        MonsterDropMoneyRowDto? dropMoney = null)
    {
        var monster = WorldDataTestRows.Monster(500) with
        {
            Life = 100,
            ItemLevel = 1,
            RealLevel = 1,
            GeneralExperience = 40,
            SummonTime1 = summonTimeSeconds,
            SummonTime2 = summonTimeSeconds,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            RadiusInfo1 = 1000,
            RadiusInfo2 = 50,
            WalkSpeed = 10,
            RunSpeed = 50
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 500) with
        {
            Number = number,
            LocationX = 100,
            LocationY = 0,
            LocationZ = 100,
            Radius = 5
        };

        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [monster],
            MonsterSpawnRegions = [region],
            MonsterDropMoney = dropMoney is null ? [] : [dropMoney]
        };

        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static Zone CreateZone(WorldDataCache cache)
    {
        var scheduler = new MonsterSpawnScheduler(cache);
        return ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);
    }

    [Fact]
    public void FirstTick_PopsEveryConfiguredSlot_Immediately()
    {
        var zone = CreateZone(CacheWithOneRegion(3));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(3, zone.MonsterCount);
    }

    [Fact]
    public void SpawnedMonster_IsPositionedWithinTheRegionRadiusOfItsHome()
    {
        var zone = CreateZone(CacheWithOneRegion());

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var monster));
        var dx = monster!.PosX - 100;
        var dz = monster.PosZ - 100;
        Assert.True(dx * dx + dz * dz <= 5 * 5 + 0.01f);
    }

    [Fact]
    public void KilledMonster_IsRemovedImmediately_AndDoesNotRespawnBeforeItsTimer()
    {
        var zone = CreateZone(CacheWithOneRegion(1, 100));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        zone.TryDamageMonster(1, 10_000, null, out var died, out _);
        Assert.True(died);

        // Drain the death (loot/respawn arming) on the next tick.
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);

        // Advance well past the respawn scan cadence (10 s) but nowhere near the 100 s respawn timer.
        for (var i = 0; i < 30; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void KilledMonster_Respawns_AfterItsTimerAndTheNextScan()
    {
        var zone = CreateZone(CacheWithOneRegion());
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        zone.TryDamageMonster(1, 10_000, null, out _, out _);

        // 2 s respawn timer + up to 10 s until the next scan boundary -- well past both.
        for (var i = 0; i < 40; i++) // 40 * 500ms = 20 s
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void MonsterKill_ByAResolvableKiller_GrantsMoneyAndExperience_AndSpawnsGroundItemsForNonMoneyDrops()
    {
        var cache = CacheWithOneRegion(dropMoney: new MonsterDropMoneyRowDto(500, 1_000_000, 100, 100));
        var zone = CreateZone(cache);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Killer", level: 1)));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);

        zone.Tick(SimulationClock.LegacyTick);

        // money is queued for the background flush host, not applied to PlayerRuntimeState directly
        var grants = zone.DrainPendingMoneyGrants();
        var grant = Assert.Single(grants);
        Assert.Equal(10, grant.CharacterId);
        Assert.True(grant.Amount > 0);

        Assert.True(zone.TryGetPlayer(10, out var killer));
        Assert.True(killer!.Experience > 0);
    }

    [Fact]
    public void MonsterKill_WithNoResolvableKiller_NeverThrows_AndGrantsNothing()
    {
        var zone = CreateZone(CacheWithOneRegion());
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        zone.TryDamageMonster(1, 10_000, 999, out _, out _);

        zone.Tick(SimulationClock.LegacyTick); // must not throw

        Assert.Empty(zone.DrainPendingMoneyGrants());
    }
}

using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterSpawnSchedulerTribeBankTaxTests
{
    private static WorldDataCache CacheWithMoneyDrop(MonsterDropMoneyRowDto dropMoney)
    {
        var monster = WorldDataTestRows.Monster(500) with
        {
            Life = 100,
            ItemLevel = 1,
            RealLevel = 1,
            GeneralExperience = 40,
            SummonTime1 = 9999,
            SummonTime2 = 9999
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 500) with
        {
            Number = 1,
            LocationX = 0,
            LocationY = 0,
            LocationZ = 0,
            Radius = 0
        };

        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [monster],
            MonsterSpawnRegions = [region],
            MonsterDropMoney = [dropMoney]
        };

        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static Zone CreateZone(WorldDataCache cache)
    {
        var scheduler = new MonsterSpawnScheduler(cache);
        return ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);
    }

    [Fact]
    public void MonsterKill_WithMoneyDrop_GrantsTheKillerButCreditsNoTribeBankTax()
    {
        var cache = CacheWithMoneyDrop(new MonsterDropMoneyRowDto(500, 1_000_000, 100, 100));
        var zone = CreateZone(cache);
        var (session, _) = ZoneTestKit.CreateSession(1);
        const byte killerTribe = 2;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Killer", level: 1, tribe: killerTribe)));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        var grant = Assert.Single(zone.DrainPendingMoneyGrants());
        Assert.Equal(10, grant.CharacterId);
        Assert.True(grant.Amount > 0);

        for (byte tribe = 0; tribe < 4; tribe++)
            Assert.Equal(0, zone.GetTribeBankTaxTotal(tribe));
    }

    [Fact]
    public void MonsterKill_WithNoMoneyDrop_CreditsNoTribeBankTax()
    {
        var cache = CacheWithMoneyDrop(new MonsterDropMoneyRowDto(0, 0, 0, 0));
        var zone = CreateZone(cache);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Killer", level: 1, tribe: 1)));
        zone.Tick(SimulationClock.LegacyTick);

        zone.TryDamageMonster(1, 10_000, 10, out _, out _);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(zone.DrainPendingMoneyGrants());
        for (byte tribe = 0; tribe < 4; tribe++)
            Assert.Equal(0, zone.GetTribeBankTaxTotal(tribe));
    }

    [Fact]
    public void MonsterKill_WithNoResolvableKiller_NeverCreditsAnyTribeBankTax()
    {
        var cache = CacheWithMoneyDrop(new MonsterDropMoneyRowDto(500, 1_000_000, 100, 100));
        var zone = CreateZone(cache);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        zone.TryDamageMonster(1, 10_000, 999, out _, out _);

        zone.Tick(SimulationClock.LegacyTick);

        for (byte tribe = 0; tribe < 4; tribe++)
            Assert.Equal(0, zone.GetTribeBankTaxTotal(tribe));
    }
}

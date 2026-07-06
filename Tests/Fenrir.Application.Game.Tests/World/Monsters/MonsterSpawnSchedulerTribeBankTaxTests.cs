using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the tribe-bank 9% monster-kill-currency tax hook wired alongside <see cref="Zone.QueueMoneyGrant" />
///     in <see cref="MonsterSpawnScheduler.ProcessDeath" /> -- see <c>Zone.TribeBankTax.cs</c> and
///     <c>TribeBankTaxAccumulator</c> for the underlying mechanics this exercises end-to-end.
/// </summary>
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
    public void MonsterKill_WithMoneyDrop_CreditsNineParcentOfTheAwardToTheKillersTribe_OnTopOfTheKillersOwnGrant()
    {
        // DropRate 500 -> always drops (RollMoney gates on RandomNumber <= (DropRate + luck) * itemDropRatio),
        // fixed MinAmount == MaxAmount so the resulting money grant is deterministic.
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

        // 9% of the exact same already-resolved amount the killer was granted -- additional to it, not a cut
        // taken out of it (the killer's own grant above is untouched).
        var expectedTax = (long)(grant.Amount * 0.09);
        Assert.Equal(expectedTax, zone.GetTribeBankTaxTotal(killerTribe));

        // No other tribe was credited.
        for (byte tribe = 0; tribe < 4; tribe++)
            if (tribe != killerTribe)
                Assert.Equal(0, zone.GetTribeBankTaxTotal(tribe));
    }

    [Fact]
    public void MonsterKill_WithNoMoneyDrop_CreditsNoTribeBankTax()
    {
        var cache = CacheWithMoneyDrop(new MonsterDropMoneyRowDto(0, 0, 0, 0)); // DropRate 0 -> RollMoney is null
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

        zone.TryDamageMonster(1, 10_000, 999, out _, out _); // 999 resolves to no player in this zone

        zone.Tick(SimulationClock.LegacyTick); // must not throw

        for (byte tribe = 0; tribe < 4; tribe++)
            Assert.Equal(0, zone.GetTribeBankTaxTotal(tribe));
    }
}

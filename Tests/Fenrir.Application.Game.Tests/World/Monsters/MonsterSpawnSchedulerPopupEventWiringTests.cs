using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterSpawnSchedulerPopupEventWiringTests
{

        private const short MonsterPopupMap = 145;

    private static WorldDataCache CacheWithOneRegion(short martialItemLevel = 0)
    {
        var monster = WorldDataTestRows.Monster(500) with
        {
            Life = 100,
            ItemLevel = 1,
            RealLevel = 1,
            GeneralExperience = 0,
            MartialItemLevel = martialItemLevel,
            SummonTime1 = 0,
            SummonTime2 = 0
        };
        var region = WorldDataTestRows.SpawnRegion(1, MonsterPopupMap, 500) with
        {
            Number = 1, LocationX = 100, LocationY = 0, LocationZ = 100, Radius = 5
        };

        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static (Zone Zone, PlayerRuntimeState Killer) SetUp(WorldDataCache cache)
    {
        var flags = new PopupEventState();
        flags.SetEnabled(PopupEventType.MonsterPve, true);
        var popupSystem = new PopupEventRewardSystem(flags);
        var scheduler = new MonsterSpawnScheduler(cache);
        var zone = ZoneTestKit.CreateZone(MonsterPopupMap, simulationSystems: [scheduler, popupSystem],
            worldData: cache);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, MonsterPopupMap, "Killer", level: 1)));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(1, zone.MonsterCount);

        Assert.True(zone.TryGetPlayer(10, out var killer));
        return (zone, killer!);
    }

        private static void KillAndRespawn(Zone zone)
    {
        zone.TryDamageMonster(1, 10_000, 10, out var died, out _);
        Assert.True(died);
        zone.Tick(TimeSpan.FromSeconds(10));
        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void EligibleKills_AdvanceThePopupCounter_AndFireTheRewardAtFourHundred()
    {
        var (zone, killer) = SetUp(CacheWithOneRegion());

        for (var i = 0; i < 399; i++)
            KillAndRespawn(zone);
        Assert.Equal(0, killer.WarPoint);

        KillAndRespawn(zone);
        Assert.Equal(1, killer.WarPoint);
    }

    [Fact]
    public void DropIneligibleKills_NeverAdvanceThePopupCounter()
    {
        var (zone, killer) = SetUp(CacheWithOneRegion(martialItemLevel: 1));

        for (var i = 0; i < 400; i++)
            KillAndRespawn(zone);

        Assert.Equal(0, killer.WarPoint);
    }
}

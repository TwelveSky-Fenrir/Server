using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterAiSystemKillCreditTests
{
    private static WorldDataCache CacheWithOneRegion()
    {
        var monster = WorldDataTestRows.Monster(600) with
        {
            Life = 1000,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            RadiusInfo1 = 2,
            RadiusInfo2 = 1000,
            WalkSpeed = 10,
            RunSpeed = 100,
            AttackType = 1
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 600) with
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

    private static Zone CreateZone(WorldDataCache cache)
    {
        var scheduler = new MonsterSpawnScheduler(cache, static () => new ZeroScatterRandom());
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0));
        var options = new GameServerOptions { AoiCellSize = 100_000f };
        return ZoneTestKit.CreateZone(1, options, simulationSystems: [scheduler, ai], worldData: cache);
    }

    private static MonsterEntity AcquireTarget(Zone zone, int expectedTargetId)
    {
        for (var i = 0; i < 10; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            if (monster!.AiState == MonsterAiState.Chase && monster.TargetCharacterId == expectedTargetId)
                return monster;
        }

        throw new InvalidOperationException("monster never acquired the expected target");
    }

    [Fact]
    public void AcquiredButNeverHitTarget_IsCreditedOnDeath_WhenDamageBypassesDamageTracking()
    {
        var zone = CreateZone(CacheWithOneRegion());
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Chased", 10, posZ: 0)));

        var monster = AcquireTarget(zone, 10);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, monster.Life, null, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(10, deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void NoAcquiredTarget_YieldsNoCredit_WhenDamageBypassesDamageTracking()
    {
        var zone = CreateZone(CacheWithOneRegion());

        MonsterEntity? monster = null;
        for (var i = 0; i < 5; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out monster));
        }

        Assert.True(zone.TryDamageMonster(monster!.ServerIndex, monster.Life, null, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Null(deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void RealDamageDealer_OutranksAcquisitionOnlyEntry_OnKillCredit()
    {
        var zone = CreateZone(CacheWithOneRegion());
        var (chasedSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(chasedSession, 1, "Chased", 10, posZ: 0)));

        var monster = AcquireTarget(zone, 10);

        var (attackerSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(attackerSession, 1, "RealAttacker", 500, posZ: 500)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out monster));
        Assert.True(zone.TryDamageMonster(monster!.ServerIndex, monster.Life, 11, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(11, deadMonster!.KillerCharacterId);
    }

    private sealed class ZeroScatterRandom : Random
    {
        public override double NextDouble()
        {
            return 0;
        }
    }
}

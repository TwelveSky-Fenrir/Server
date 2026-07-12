using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterAiSystemTests
{
    private static WorldDataCache CacheWithOneRegion(short frameInfo1, short frameInfo3, short radiusInfo1,
        short radiusInfo2, short walkSpeed, short runSpeed, float regionRadius, byte attackType = 1,
        short frameInfo6 = 1)
    {
        var monster = WorldDataTestRows.Monster(600) with
        {
            Life = 1000,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = frameInfo1,
            FrameInfo3 = frameInfo3,
            FrameInfo6 = frameInfo6,
            RadiusInfo1 = radiusInfo1,
            RadiusInfo2 = radiusInfo2,
            WalkSpeed = walkSpeed,
            RunSpeed = runSpeed,
            AttackType = attackType
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 600) with
        {
            Number = 1,
            LocationX = 0,
            LocationY = 0,
            LocationZ = 0,
            Radius = (int)regionRadius
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

    [Fact]
    public void Monster_Spawning_ConvertsFrameInfo1AtThirtyUnitsPerSecond_NotOneUnitPerLegacyTick()
    {
        var zone = CreateZone(CacheWithOneRegion(40, 1, 0, 0,
            0, 0, 0));

        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(MonsterAiState.Spawning, monster!.AiState);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(MonsterAiState.Spawning, monster.AiState);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(MonsterAiState.Decision, monster.AiState);
    }

    [Fact]
    public void Monster_WithNoNearbyPlayer_StaysIdleAtHome()
    {
        var zone = CreateZone(CacheWithOneRegion(1, 1, 5, 50,
            10, 10, 0));

        for (var i = 0; i < 5; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(MonsterAiState.Decision, monster!.AiState);
        Assert.Equal(0, monster.PosX);
        Assert.Equal(0, monster.PosZ);
    }

    [Fact]
    public void Monster_NonAggressiveAttackType_NeverDetectsEvenAnAdjacentPlayer()
    {
        var zone = CreateZone(CacheWithOneRegion(1, 1, 1000, 1000,
            10, 1000, 50, 2));
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 1, posZ: 0)));

        for (var i = 0; i < 10; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.NotEqual(MonsterAiState.Chase, monster!.AiState);
        Assert.NotEqual(MonsterAiState.AttackWindup, monster.AiState);
    }

    [Fact]
    public void Monster_DetectsNearbyPlayer_ChasesAndEventuallyAttacks()
    {
        var zone = CreateZone(CacheWithOneRegion(1, 1, 2, 1000,
            10, 1000, 50));
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 10, posZ: 0)));

        var reachedAttackWindup = false;
        for (var i = 0; i < 10 && !reachedAttackWindup; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            if (monster!.AiState == MonsterAiState.AttackWindup)
                reachedAttackWindup = true;
        }

        Assert.True(reachedAttackWindup, "monster never reached AttackWindup after detecting a nearby player");
    }

    [Fact]
    public void Monster_AttackWindup_ReturnsToDecision_AfterFrameInfo3Ticks()
    {
        var zone = CreateZone(CacheWithOneRegion(1, 2, 1000, 1000,
            10, 1000, 0));
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 10, posZ: 0)));

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock
            .LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(MonsterAiState.AttackWindup, monster!.AiState);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(MonsterAiState.AttackWindup, monster.AiState);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(MonsterAiState.Decision, monster.AiState);
    }

    [Fact]
    public void Monster_TargetEscapesDetectionRadiusMidChase_GivesUpToIdle()
    {
        var zone = CreateZone(CacheWithOneRegion(1, 1, 2, 50,
            10, 1000, 50));
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 20, posZ: 0)));

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock
            .LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var chasing));
        Assert.Equal(MonsterAiState.Chase, chasing!.AiState);

        Assert.True(zone.TryGetPlayer(10, out var target));
        target!.PosX = 5000f;
        target.PosZ = 0f;

        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var gaveUp));
        Assert.Equal(MonsterAiState.Decision, gaveUp!.AiState);
        Assert.Null(gaveUp.TargetCharacterId);
    }

    [Fact]
    public void Monster_NoWorldGeometryLoaded_IdleReturnHomeCheckNeverForcesRecall()
    {
        var zone = CreateZone(CacheWithOneRegion(1, 1, 2, 50,
            10, 1000, 50));
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 20, posZ: 0)));

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock
            .LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(10, out var target));
        target!.PosX = 5000f;
        target.PosZ = 0f;

        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var gaveUp));
        Assert.Equal(MonsterAiState.Decision, gaveUp!.AiState);

        for (var i = 0; i < 200; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            Assert.NotEqual(MonsterAiState.ReturnToSpawn, monster!.AiState);
        }
    }

    [Fact]
    public void Monster_LosesTarget_ReacquiresNearbyReplacementDuringGracePeriod_InsteadOfHeadingHome()
    {
        var zone = CreateZone(CacheWithOneRegion(1, 1, 2, 50,
            10, 1000, 50));
        var (originalSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(originalSession, 1, "Original", 20, posZ: 0)));

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock
            .LegacyTick);
        zone.Tick(SimulationClock
            .LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var chasing));
        Assert.Equal(MonsterAiState.Chase, chasing!.AiState);
        Assert.Equal(10, chasing.TargetCharacterId);

        Assert.True(zone.TryGetPlayer(10, out var original));
        original!.PosX = 5000f;
        original.PosZ = 0f;
        var (replacementSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(replacementSession, 1, "Replacement", 5, posZ: 0)));

        zone.Tick(SimulationClock
            .LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var gaveUp));
        Assert.Equal(MonsterAiState.Decision, gaveUp!.AiState);
        Assert.Null(gaveUp.TargetCharacterId);

        var reacquired = false;
        for (var i = 0; i < 5 && !reacquired; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            if (monster!.AiState == MonsterAiState.Chase)
                reacquired = true;
        }

        Assert.True(reacquired,
            "monster never re-engaged the still-nearby replacement target during the idle re-detection grace period");
        Assert.True(zone.TryGetMonster(1, out var reengaged));
        Assert.Equal(11, reengaged!.TargetCharacterId);
    }

    [Fact]
    public void Monster_ReturnToSpawn_ConvertsFrameInfo6AtThirtyUnitsPerSecond_NotOneUnitPerLegacyTick()
    {
        var zone = CreateZone(CacheWithOneRegion(1, 1, 0, 0, 0, 0, 0, frameInfo6: 30));

        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var monster));

        monster!.AiState = MonsterAiState.ReturnToSpawn;
        monster.StateTicks = 0;
        monster.StateFrameAccumulator = 0f;
        monster.PosX = monster.HomeX + 500f;
        monster.PosZ = monster.HomeZ + 500f;

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(MonsterAiState.ReturnToSpawn, monster.AiState);
        Assert.NotEqual(monster.HomeX, monster.PosX);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(MonsterAiState.Spawning, monster.AiState);
        Assert.Equal(monster.HomeX, monster.PosX);
        Assert.Equal(monster.HomeZ, monster.PosZ);
    }

    private sealed class ZeroScatterRandom : Random
    {
        public override double NextDouble()
        {
            return 0;
        }
    }
}

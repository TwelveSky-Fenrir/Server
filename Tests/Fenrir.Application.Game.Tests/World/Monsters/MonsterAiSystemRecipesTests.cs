using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterAiSystemRecipesTests
{
    [Fact]
    public void SpecialSort_OrdinaryMonster_DerivesToStandard()
    {
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(1, 1));
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(2, 7));

        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(1, 40));
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(1, 44));
    }

    [Theory]
    [InlineData((byte)11)]
    [InlineData((byte)12)]
    [InlineData((byte)13)]
    [InlineData((byte)28)]
    [InlineData((byte)14)]
    [InlineData((byte)15)]
    public void SpecialSort_TribeSymbolSpecialTypes_DeriveToTribeSymbolStone(byte specialType)
    {
        Assert.Equal(MonsterSpecialSort.TribeSymbolStone, MonsterSpecialSort.Derive(1, specialType));
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(2, specialType));
    }

    [Fact]
    public void SpecialSort_TowerSpecialType_DerivesToTower()
    {
        Assert.Equal(MonsterSpecialSort.Tower, MonsterSpecialSort.Derive(1, 10));
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(2, 10));
    }

    [Theory]
    [InlineData((byte)21)]
    [InlineData((byte)22)]
    [InlineData((byte)23)]
    [InlineData((byte)29)]
    public void SpecialSort_InertSpecialTypes_DeriveToInert(byte specialType)
    {
        Assert.Equal(MonsterSpecialSort.Inert, MonsterSpecialSort.Derive(1, specialType));
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(2, specialType));
    }

    [Theory]
    [InlineData((byte)31)]
    [InlineData((byte)32)]
    [InlineData((byte)33)]
    [InlineData((byte)34)]
    public void SpecialSort_AllianceStoneSpecialTypes_DeriveToAllianceStone(byte specialType)
    {
        Assert.Equal(MonsterSpecialSort.AllianceStone, MonsterSpecialSort.Derive(1, specialType));
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(2, specialType));
    }

    [Theory]
    [InlineData((byte)18)]
    [InlineData((byte)35)]
    [InlineData((byte)36)]
    [InlineData((byte)37)]
    [InlineData((byte)38)]
    public void SpecialSort_CarThrowerSpecialTypes_DeriveToCarThrower(byte specialType)
    {
        Assert.Equal(MonsterSpecialSort.CarThrower, MonsterSpecialSort.Derive(1, specialType));
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(2, specialType));
    }

    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    [InlineData((byte)8)]
    [InlineData((byte)9)]
    public void SpecialSort_TribeGuardTypes_DeriveToTribeGuard_RegardlessOfSpecialType(byte type)
    {
        Assert.Equal(MonsterSpecialSort.TribeGuard, MonsterSpecialSort.Derive(type, 11));
        Assert.Equal(MonsterSpecialSort.TribeGuard, MonsterSpecialSort.Derive(type, 0));
    }

    [Fact]
    public void SpecialSort_IsSeededOntoTheEntityAtSpawn()
    {
        var template = WorldDataTestRows.Monster(600) with { Type = 1, SpecialType = 12 };
        var entity = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0);
        Assert.Equal(MonsterSpecialSort.TribeSymbolStone, entity.SpecialSort);

        var overridden = MonsterEntity.Create(2, 2, template, 1, 0, 0, 0,
            specialSort: MonsterSpecialSort.CarThrower);
        Assert.Equal(MonsterSpecialSort.CarThrower, overridden.SpecialSort);
    }


    [Fact]
    public void ThrowCar_TargetInsideTheAnnulusBand_CommitsMeleeAttack()
    {
        var (zone, monster) = CreateZoneWithManualMonster(100, new ScriptedRandomSource(5, 0));
        EnterPlayerAt(zone, 10, 50, 0);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(MonsterAiState.Decision, monster.AiState);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.Equal(MonsterAiState.AttackWindup, monster.AiState);
        Assert.Equal(10, monster.TargetCharacterId);
    }

    [Fact]
    public void ThrowCar_TargetInsideTheInnerDeadZone_IsNotAcquired()
    {
        var (zone, monster) = CreateZoneWithManualMonster(100, new ScriptedRandomSource(5));
        EnterPlayerAt(zone, 10, 10, 0);

        for (var i = 0; i < 5; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.NotEqual(MonsterAiState.AttackWindup, monster.AiState);
        Assert.Null(monster.TargetCharacterId);
    }

    [Fact]
    public void ThrowCar_WanderTrigger_EntersPatrol()
    {
        var (zone, monster) = CreateZoneWithManualMonster(100, new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, 50, 0);

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(MonsterAiState.Patrol, monster.AiState);
    }

    [Fact]
    public void ThrowCar_WanderTrigger_WithoutAnyNearbyReadyPlayer_StaysIdle()
    {
        var (zone, monster) = CreateZoneWithManualMonster(100, new ScriptedRandomSource(0));

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(MonsterAiState.Decision, monster.AiState);
    }

    [Fact]
    public void ThrowCar_WanderTrigger_WithOnlyANotYetReadyPlayer_StaysIdle()
    {
        var (zone, monster) = CreateZoneWithManualMonster(100, new ScriptedRandomSource(0));
        var (session, _) = ZoneTestKit.CreateSession(10);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "P10", 50f, 0f, 0f)));

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(MonsterAiState.Decision, monster.AiState);
    }

    [Fact]
    public void ThrowCar_WanderTrigger_WithOnlyAMovingZonePlayer_StaysIdle()
    {
        var (zone, monster) = CreateZoneWithManualMonster(100, new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, 50, 0);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetPlayer(10, out var player));
        player!.IsMovingZone = true;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(MonsterAiState.Decision, monster.AiState);
    }

    [Fact]
    public void ThrowCar_WanderTrigger_WithOnlyAPlayerBeyondTheCoarseCellTolerance_StaysIdle()
    {
        var options = new GameServerOptions { AoiCellSize = 100f };
        var template = WorldDataTestRows.Monster(600) with
        {
            Life = 100_000,
            AttackType = 1,
            RadiusInfo1 = 100,
            WalkSpeed = 10,
            RunSpeed = 100,
            FrameInfo1 = 1,
            FrameInfo3 = 2
        };
        var monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0,
            specialSort: MonsterSpecialSort.CarThrower);

        var zone = ZoneTestKit.CreateZone(1, options, simulationSystems: [new MonsterAiSystem(new ScriptedRandomSource(0))]);
        zone.SpawnMonster(monster);
        EnterPlayerAt(zone, 10, 301f, 0);

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(MonsterAiState.Decision, monster.AiState);
    }


    [Fact]
    public void Zone175Boss_MultiCandidate_WritesEveryInRangeCandidateToTheSharedTable()
    {
        var zone = CreateBossZone();
        EnterPlayerAt(zone, 10, 5, 0);
        EnterPlayerAt(zone, 11, 8, 0);

        var boss = AcquireBossTarget(zone);
        var lockedId = boss.TargetCharacterId!.Value;
        var nonLockedId = lockedId == 10 ? 11 : 10;

        Assert.True(zone.TryGetMonster(boss.ServerIndex, out var live));
        Assert.True(zone.TryDamageMonster(live!.ServerIndex, 500, nonLockedId, out var died, out var remaining));
        Assert.False(died);

        Assert.True(zone.TryDamageMonster(live.ServerIndex, remaining, null, out var killed, out _));
        Assert.True(killed);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(nonLockedId, deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void Zone175Boss_NoNearbyPlayer_StaysIdle()
    {
        var zone = CreateBossZone();

        MonsterEntity? boss = null;
        for (var i = 0; i < 6; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out boss));
        }

        Assert.Equal(MonsterAiState.Decision, boss!.AiState);
        Assert.Null(boss.TargetCharacterId);
    }

    [Fact]
    public void Zone175Boss_ZeroDetectionRadius_StillAcquiresACoLocatedPlayer()
    {
        var zone = CreateBossZone(wideRadius: 0, meleeRadius: 0);
        EnterPlayerAt(zone, 10, 0, 0);

        var boss = AcquireBossTarget(zone);

        Assert.Equal(10, boss.TargetCharacterId);
    }


    [Fact]
    public void Chase_ShortRangeRetarget_SharesThrottleClockWithIdleAcquisitionScan()
    {
        var zone = CreateStandardChaseZone();
        EnterPlayerAt(zone, 10, 500, 0);
        EnterPlayerAt(zone, 11, 1, 0);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var monster));
        monster!.AssignTarget(10, 0, 500f, 0f, 0f);
        monster.AiState = MonsterAiState.Chase;
        monster.DetectionThrottleTicks = SimulationClock.MonsterDetectionThrottleLegacyTicks - 1;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var updated));
        Assert.Equal(11, updated!.TargetCharacterId);
    }

    [Fact]
    public void AdjustValidAttackTarget_DropsAMidCrossShardTransferTarget()
    {
        var zone = CreateStandardChaseZone();
        EnterPlayerAt(zone, 10, 20, 0);

        var monster = AcquireChaseTarget(zone, 10);

        Assert.True(zone.TryGetPlayer(10, out var target));
        target!.IsMovingZone = true;

        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var pruned));
        Assert.Equal(MonsterAiState.Decision, pruned!.AiState);
        Assert.Null(pruned.TargetCharacterId);
    }


    private static (Zone Zone, MonsterEntity Monster) CreateZoneWithManualMonster(short meleeRadius,
        IRandomSource ai)
    {
        var template = WorldDataTestRows.Monster(600) with
        {
            Life = 100_000,
            AttackType = 1,
            RadiusInfo1 = meleeRadius,
            WalkSpeed = 10,
            RunSpeed = 100,
            FrameInfo1 = 1,
            FrameInfo3 = 2
        };
        var monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0,
            specialSort: MonsterSpecialSort.CarThrower);

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options, simulationSystems: [new MonsterAiSystem(ai)]);
        zone.SpawnMonster(monster);
        return (zone, monster);
    }

    private static Zone CreateBossZone(short wideRadius = 1000, short meleeRadius = 2)
    {
        var monster = WorldDataTestRows.Monster(600) with
        {
            SpecialType = 40,
            Life = 1_000_000,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo4 = 2,
            RadiusInfo1 = meleeRadius,
            RadiusInfo2 = wideRadius,
            WalkSpeed = 10,
            RunSpeed = 100,
            AttackType = 1
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 600) with
        {
            Number = 1, LocationX = 0, LocationY = 0, LocationZ = 0, Radius = 0
        };
        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        var scheduler = new MonsterSpawnScheduler(cache, static () => new ZeroScatterRandom());
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0));
        var options = new GameServerOptions { AoiCellSize = 100_000f };
        return ZoneTestKit.CreateZone(1, options, simulationSystems: [scheduler, ai], worldData: cache);
    }

    private static Zone CreateStandardChaseZone()
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
            Number = 1, LocationX = 0, LocationY = 0, LocationZ = 0, Radius = 0
        };
        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        var scheduler = new MonsterSpawnScheduler(cache, static () => new ZeroScatterRandom());
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0));
        var options = new GameServerOptions { AoiCellSize = 100_000f };
        return ZoneTestKit.CreateZone(1, options, simulationSystems: [scheduler, ai], worldData: cache);
    }

    private static void EnterPlayerAt(Zone zone, int characterId, float posX, float posZ)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        session.MarkInWorld();
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, 1, $"P{characterId}", posX, 0f, posZ)));
    }

    private static MonsterEntity AcquireBossTarget(Zone zone)
    {
        for (var i = 0; i < 10; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var boss));
            if (boss!.TargetCharacterId is not null)
                return boss;
        }

        throw new InvalidOperationException("Zone175 boss never acquired a target");
    }

    private static MonsterEntity AcquireChaseTarget(Zone zone, int expectedTargetId)
    {
        for (var i = 0; i < 10; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            if (monster!.AiState == MonsterAiState.Chase && monster.TargetCharacterId == expectedTargetId)
                return monster;
        }

        throw new InvalidOperationException("monster never acquired the expected chase target");
    }

    private sealed class ZeroScatterRandom : Random
    {
        public override double NextDouble()
        {
            return 0;
        }
    }
}

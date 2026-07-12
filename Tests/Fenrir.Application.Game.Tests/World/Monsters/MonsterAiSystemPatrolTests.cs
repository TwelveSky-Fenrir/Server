using System.Numerics;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterAiSystemPatrolTests
{
    [Fact]
    public void AlreadyTrackedAttacker_InterruptsPatrol_ToDecisionWithoutMoving()
    {
        var (zone, monster) = CreatePatrollingZone();
        EnterPlayerAt(zone, 10, 5, 0);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5, 10, out var died, out _));
        Assert.False(died);

        monster.AiState = MonsterAiState.Patrol;
        var posXBeforeInterrupt = monster.PosX;
        var posZBeforeInterrupt = monster.PosZ;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Equal(posXBeforeInterrupt, live.PosX);
        Assert.Equal(posZBeforeInterrupt, live.PosZ);
    }

    [Fact]
    public void FreshlyAcquirableAttacker_InterruptsPatrol_ToDecisionWithoutMoving()
    {
        var (zone, monster) = CreatePatrollingZone();
        EnterPlayerAt(zone, 10, 5, 0);
        monster.DetectionThrottleTicks = SimulationClock.MonsterDetectionThrottleLegacyTicks;

        var posXBeforeInterrupt = monster.PosX;
        var posZBeforeInterrupt = monster.PosZ;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Equal(10, live.TargetCharacterId);
        Assert.Equal(posXBeforeInterrupt, live.PosX);
        Assert.Equal(posZBeforeInterrupt, live.PosZ);
    }

    [Fact]
    public void NonStandardSpecialSort_IsNeverInterruptedByAnAttacker_KeepsPatrolling()
    {
        var (zone, monster) = CreatePatrollingZone(MonsterSpecialSort.TribeGuard);
        EnterPlayerAt(zone, 10, 5, 0);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5, 10, out var died, out _));
        Assert.False(died);

        monster.AiState = MonsterAiState.Patrol;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(MonsterAiState.Patrol, live!.AiState);
    }

    [Fact]
    public void UnreachableWanderTarget_EventuallyFallsBackToDecision()
    {
        var (zone, monster) = CreatePatrollingZone(geometry: TwoIslandGeometry());
        monster.WanderTargetX = 100f;
        monster.WanderTargetZ = 100f;

        var reachedDecision = false;
        for (var i = 0; i < 10 && !reachedDecision; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
            if (live!.AiState == MonsterAiState.Decision)
                reachedDecision = true;
        }

        Assert.True(reachedDecision, "monster never fell back to Decision after its wander path was blocked");
    }

    [Fact]
    public void ReachableWanderTarget_EventuallyArrives_ToDecisionWithoutAnAttacker()
    {
        var (zone, monster) = CreatePatrollingZone();

        var reachedDecision = false;
        for (var i = 0; i < 20 && !reachedDecision; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
            if (live!.AiState == MonsterAiState.Decision)
                reachedDecision = true;
        }

        Assert.True(reachedDecision, "monster never arrived at its wander target and returned to Decision");
    }

    [Fact]
    public void StandardIdleWander_CommitsToPatrol_WithFacingTowardTheRolledTarget()
    {
        var template = WorldDataTestRows.Monster(600) with
        {
            Life = 1000,
            AttackType = 0,
            WalkSpeed = 10,
            RunSpeed = 10,
            FrameInfo1 = 1
        };
        var monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0, 500);
        monster.AiState = MonsterAiState.Decision;
        monster.IdleWanderElapsedTicks = SimulationClock.MonsterIdleWanderLegacyTicks;

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options,
            simulationSystems: [new MonsterAiSystem(new ScriptedRandomSource(0))]);
        zone.SpawnMonster(monster);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(monster.ServerIndex, out var live));
        Assert.Equal(MonsterAiState.Patrol, live!.AiState);

        var expectedHeading = MathF.Atan2(live.WanderTargetX - live.PosX, live.WanderTargetZ - live.PosZ);
        Assert.NotEqual(0f, live.Heading);
        Assert.Equal(expectedHeading, live.Heading, 4);
    }

    private static (Zone Zone, MonsterEntity Monster) CreatePatrollingZone(
        byte specialSort = MonsterSpecialSort.Standard, ZoneGeometry? geometry = null)
    {
        var template = WorldDataTestRows.Monster(600) with
        {
            Life = 1000,
            AttackType = 1,
            RadiusInfo1 = 5,
            RadiusInfo2 = 1000,
            WalkSpeed = 10,
            RunSpeed = 10,
            FrameInfo1 = 1
        };
        var monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0, 500, specialSort: specialSort);
        monster.AiState = MonsterAiState.Patrol;
        monster.WanderTargetX = 500f;
        monster.WanderTargetZ = 0f;

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options, simulationSystems: [new MonsterAiSystem(new ScriptedRandomSource(0))],
            geometry: geometry);
        zone.SpawnMonster(monster);
        return (zone, monster);
    }

    private static void EnterPlayerAt(Zone zone, int characterId, float posX, float posZ)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, 1, $"P{characterId}", posX, 0f, posZ)));
    }

    private static ZoneGeometry TwoIslandGeometry()
    {
        const float groundY = 10f;
        var plane = new Vector4(0f, 1f, 0f, groundY);

        var islandA1 = new WorldTriangle(new Vector3(-10, groundY, -10), new Vector3(10, groundY, -10),
            new Vector3(10, groundY, 10), plane);
        var islandA2 = new WorldTriangle(new Vector3(-10, groundY, -10), new Vector3(10, groundY, 10),
            new Vector3(-10, groundY, 10), plane);

        var islandB1 = new WorldTriangle(new Vector3(90, groundY, 90), new Vector3(110, groundY, 90),
            new Vector3(110, groundY, 110), plane);
        var islandB2 = new WorldTriangle(new Vector3(90, groundY, 90), new Vector3(110, groundY, 110),
            new Vector3(90, groundY, 110), plane);

        var triangles = new[] { islandA1, islandA2, islandB1, islandB2 };
        var root = new QuadtreeNode(new Vector3(-10, 0, -10), new Vector3(110, groundY, 110), [0, 1, 2, 3],
            [-1, -1, -1, -1]);
        return new ZoneGeometry(triangles, [root]);
    }
}

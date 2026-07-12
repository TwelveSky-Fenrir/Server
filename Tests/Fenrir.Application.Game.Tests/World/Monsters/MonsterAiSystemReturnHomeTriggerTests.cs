using System.Numerics;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterAiSystemReturnHomeTriggerTests
{
    private static (Zone Zone, MonsterEntity Monster) CreateIdleZone(ZoneGeometry? geometry, float homeX,
        float homeY, float homeZ, short walkSpeed = 100)
    {
        var template = WorldDataTestRows.Monster(600) with
        {
            Life = 100_000,
            AttackType = 1,
            RadiusInfo1 = 5,
            RadiusInfo2 = 50,
            WalkSpeed = walkSpeed,
            RunSpeed = walkSpeed,
            FollowInfo1 = 5,
            FollowInfo2 = 5,
            FrameInfo1 = 1,
            FrameInfo6 = 1
        };
        var monster = MonsterEntity.Create(1, 1, template, 1, homeX, homeY, homeZ);
        monster.AiState = MonsterAiState.Decision;

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options,
            simulationSystems: [new MonsterAiSystem(new ScriptedRandomSource(0))], geometry: geometry);
        zone.SpawnMonster(monster);
        return (zone, monster);
    }

    private static ZoneGeometry FlatGeometry()
    {
        const float groundY = 10f;
        var plane = new Vector4(0f, 1f, 0f, groundY);
        var triangles = new[]
        {
            new WorldTriangle(new Vector3(-1000, groundY, -1000), new Vector3(1000, groundY, -1000),
                new Vector3(1000, groundY, 1000), plane),
            new WorldTriangle(new Vector3(-1000, groundY, -1000), new Vector3(1000, groundY, 1000),
                new Vector3(-1000, groundY, 1000), plane)
        };
        var root = new QuadtreeNode(new Vector3(-1000, 0, -1000), new Vector3(1000, groundY, 1000), [0, 1],
            [-1, -1, -1, -1]);
        return new ZoneGeometry(triangles, [root]);
    }

    private static ZoneGeometry FlatGroundlessGeometry()
    {
        const float vertexY = 10f;
        var plane = new Vector4(0f, -1f, 0f, vertexY);
        var triangles = new[]
        {
            new WorldTriangle(new Vector3(-1000, vertexY, -1000), new Vector3(1000, vertexY, -1000),
                new Vector3(1000, vertexY, 1000), plane),
            new WorldTriangle(new Vector3(-1000, vertexY, -1000), new Vector3(1000, vertexY, 1000),
                new Vector3(-1000, vertexY, 1000), plane)
        };
        var root = new QuadtreeNode(new Vector3(-1000, 0, -1000), new Vector3(1000, vertexY, 1000), [0, 1],
            [-1, -1, -1, -1]);
        return new ZoneGeometry(triangles, [root]);
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

    [Fact]
    public void UnobstructedHomePath_SixtySecondCheck_ResetsTimerWithoutEnteringReturnToSpawn()
    {
        var (zone, monster) = CreateIdleZone(FlatGeometry(), 0f, 10f, 0f);
        monster.PosX = 300f;
        monster.PosY = 10f;
        monster.PosZ = 0f;
        monster.IdleReturnElapsedTicks = SimulationClock.MonsterIdleReturnHomeLegacyTicks;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Equal(0, live.IdleReturnElapsedTicks);
        Assert.Equal(300f, live.PosX);
        Assert.Equal(0f, live.PosZ);
    }

    [Fact]
    public void UnobstructedHomePath_SixtySecondCheck_AlreadyHome_FallsThroughToWanderCheckSameTick()
    {
        var (zone, monster) = CreateIdleZone(FlatGeometry(), 0f, 10f, 0f);
        monster.PosX = 300f;
        monster.PosY = 10f;
        monster.PosZ = 0f;
        monster.IdleReturnElapsedTicks = SimulationClock.MonsterIdleReturnHomeLegacyTicks;
        monster.IdleWanderElapsedTicks = SimulationClock.MonsterIdleWanderLegacyTicks - 1;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Patrol, live!.AiState);
        Assert.Equal(0, live.IdleReturnElapsedTicks);
        Assert.Equal(0, live.IdleWanderElapsedTicks);
    }

    [Fact]
    public void UnobstructedHomePath_SixtySecondCheck_AlreadyHome_WanderNotYetDue_StaysIdleWithoutForcedReset()
    {
        var (zone, monster) = CreateIdleZone(FlatGeometry(), 0f, 10f, 0f);
        monster.PosX = 300f;
        monster.PosY = 10f;
        monster.PosZ = 0f;
        monster.IdleReturnElapsedTicks = SimulationClock.MonsterIdleReturnHomeLegacyTicks;
        monster.IdleWanderElapsedTicks = 5;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Equal(0, live.IdleReturnElapsedTicks);
        Assert.Equal(6, live.IdleWanderElapsedTicks);
    }

    [Fact]
    public void XzObstructedHomePath_SixtySecondCheck_EntersReturnToSpawn_WithoutImmediatelyTeleporting()
    {
        var (zone, monster) = CreateIdleZone(TwoIslandGeometry(), 0f, 10f, 0f);
        monster.PosX = 100f;
        monster.PosY = 10f;
        monster.PosZ = 100f;
        monster.IdleReturnElapsedTicks = SimulationClock.MonsterIdleReturnHomeLegacyTicks;
        monster.IdleWanderElapsedTicks = 7;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.ReturnToSpawn, live!.AiState);
        Assert.Equal(0, live.IdleReturnElapsedTicks);
        Assert.Equal(0, live.IdleWanderElapsedTicks);
        Assert.Equal(100f, live.PosX);
        Assert.Equal(100f, live.PosZ);
    }

    [Fact]
    public void GroundHeightUnresolvableAtHomePath_SixtySecondCheck_CollapsesToCurrentPosition_StillEntersReturnToSpawn()
    {
        var (zone, monster) = CreateIdleZone(FlatGroundlessGeometry(), 0f, 10f, 0f);
        monster.PosX = 300f;
        monster.PosY = 10f;
        monster.PosZ = 0f;
        monster.IdleReturnElapsedTicks = SimulationClock.MonsterIdleReturnHomeLegacyTicks;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.ReturnToSpawn, live!.AiState);
        Assert.Equal(0, live.IdleReturnElapsedTicks);
        Assert.Equal(300f, live.PosX);
        Assert.Equal(0f, live.PosZ);
    }
}

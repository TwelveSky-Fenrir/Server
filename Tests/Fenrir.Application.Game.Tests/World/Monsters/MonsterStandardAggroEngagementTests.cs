using System.Numerics;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterStandardAggroEngagementTests
{

        private static (Zone Zone, MonsterEntity Monster) CreateStandardZone(short meleeRadius, short leashRadius,
        short runSpeed, IRandomSource ai, ZoneGeometry? geometry = null, bool startInSpawning = false)
    {
        var template = WorldDataTestRows.Monster(600) with
        {
            Life = 100_000,
            AttackType = 1,
            RadiusInfo1 = meleeRadius,
            RadiusInfo2 = leashRadius,
            WalkSpeed = 10,
            RunSpeed = runSpeed,
            FollowInfo1 = 5,
            FollowInfo2 = 5,
            FrameInfo1 = 100
        };
        var monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0, 50);
        if (!startInSpawning)
            monster.AiState = MonsterAiState.Decision;

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options, simulationSystems: [new MonsterAiSystem(ai)],
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
    public void TrackedAttackerTableAlreadyNonEmpty_SkipsTheProximityScanEntirely_AndEngagesImmediately()
    {
        var (zone, monster) = CreateStandardZone(meleeRadius: 1000, leashRadius: 1000, runSpeed: 100,
            ai: new ScriptedRandomSource(0), startInSpawning: true);
        EnterPlayerAt(zone, 10, posX: 5, posZ: 0);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5, 10, out var died, out _));
        Assert.False(died);

        monster.AiState = MonsterAiState.Decision;
        monster.StateTicks = 0;

        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.AttackWindup, live!.AiState);
        Assert.Equal(10, live.TargetCharacterId);
    }

    [Fact]
    public void EmptyTableAndFailedScan_NeverReachesEngagementStep_StaysIdle()
    {
        var (zone, _) = CreateStandardZone(meleeRadius: 10, leashRadius: 50, runSpeed: 100,
            ai: new ScriptedRandomSource(0));

        for (var i = 0; i < 5; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Null(live.TargetCharacterId);
    }

    [Fact]
    public void AllTrackedAttackersInvalidated_EmptyAfterPrune_NoTransitionAtAll()
    {
        var (zone, monster) = CreateStandardZone(meleeRadius: 1000, leashRadius: 1000, runSpeed: 100,
            ai: new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, posX: 5, posZ: 0);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5, 10, out var died, out _));
        Assert.False(died);

        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.IsDead = true;

        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Null(live.TargetCharacterId);
    }

    [Fact]
    public void BeyondMeleeRangeSurvivor_NonPositiveChaseSpeed_NoTransitionAtAll()
    {
        var (zone, _) = CreateStandardZone(meleeRadius: 5, leashRadius: 1000, runSpeed: 0,
            ai: new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, posX: 100, posZ: 0);
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState);
        Assert.Equal(0f, live.PosX);
        Assert.Equal(0f, live.PosZ);
    }

    [Fact]
    public void BeyondMeleeRangeSurvivor_ReachableApproachPoint_ChasesTowardAnArcOffsetPoint()
    {
        var (zone, _) = CreateStandardZone(meleeRadius: 10, leashRadius: 1000, runSpeed: 100,
            ai: new ScriptedRandomSource(0, 0,
                5, 1),
            geometry: FlatGeometry());
        EnterPlayerAt(zone, 10, posX: 100, posZ: 0);
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Chase, live!.AiState);
        Assert.Equal(10, live.TargetCharacterId);

        var approachDistance = MathF.Sqrt(live.TargetLocationX * live.TargetLocationX +
                                           live.TargetLocationZ * live.TargetLocationZ);
        Assert.True(MathF.Abs(approachDistance - 100f) < 0.01f,
            $"approach point should sit exactly 100 units from the monster, was {approachDistance}");

        Assert.True(live.TargetLocationZ > 0.01f,
            $"approach point should be laterally offset off the X axis, was ({live.TargetLocationX},{live.TargetLocationZ})");
    }

    [Fact]
    public void BeyondMeleeRangeSurvivor_UnreachableApproachPoint_TransitionsToReturnToSpawn()
    {
        var (zone, _) = CreateStandardZone(meleeRadius: 5, leashRadius: 2000, runSpeed: 100,
            ai: new ScriptedRandomSource(0), geometry: TwoIslandGeometry());
        EnterPlayerAt(zone, 10, posX: 100, posZ: 100);
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.ReturnToSpawn, live!.AiState);
    }
}

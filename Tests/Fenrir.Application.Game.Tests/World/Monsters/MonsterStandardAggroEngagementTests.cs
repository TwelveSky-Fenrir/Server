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

/// <summary>
///     Covers the recovered <c>monster-ai-recipe-bodies</c> behavior contract's first sub-gap (behavior
///     contract <c>A3-aggro-pruning</c>): the Standard recipe's own post-prune engagement selection --
///     <see cref="MonsterAggroListPruner.Prune" /> finally wired into <see cref="MonsterAiSystem" />'s decision
///     tick, the "skip the scan when the table is already non-empty" Trigger gate, the per-entry melee coin
///     flip / uniform fallback survivor selection, the arc-shaped chase approach point, and the
///     reachability-gated chase-vs-walk-home branch. Everything is observed through <see cref="Zone" />'s
///     public surface only, matching the sibling <c>MonsterAiSystem*Tests</c> files.
/// </summary>
public class MonsterStandardAggroEngagementTests
{
    /// <summary>
    ///     A manually-spawned Standard-recipe monster (no spawn scheduler needed) parked at the origin, already
    ///     in <see cref="MonsterAiState.Decision" /> (spawn-wait already elapsed) -- the same "manual monster +
    ///     AI" idiom <c>MonsterAiSystemMidChaseRetargetTests</c>/<c>MonsterAiSystemRecipesTests</c> already
    ///     established.
    /// </summary>
    private static (Zone Zone, MonsterEntity Monster) CreateStandardZone(short meleeRadius, short leashRadius,
        short runSpeed, IRandomSource ai, ZoneGeometry? geometry = null, bool startInSpawning = false)
    {
        var template = WorldDataTestRows.Monster(600) with
        {
            Life = 100_000,
            AttackType = 1, // proactive-aggro gate: AttackType in {1,3,6}
            RadiusInfo1 = meleeRadius,
            RadiusInfo2 = leashRadius,
            WalkSpeed = 10,
            RunSpeed = runSpeed,
            FollowInfo1 = 5,
            FollowInfo2 = 5,
            // Kept large so a fixture that deliberately starts in Spawning (see startInSpawning) never
            // transitions to Decision on its own during setup ticks -- the test forces that transition
            // explicitly, at the exact moment it wants to observe the FIRST-ever Decision dispatch.
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

    /// <summary>
    ///     A flat, single-triangle-pair square around the origin, big enough to contain every straight-line
    ///     approach point these fixtures compute (lateral offset never exceeds a small melee radius).
    /// </summary>
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

    /// <summary>
    ///     Two small squares far apart sharing no vertex/edge -- both on-mesh individually, but NOT
    ///     navmesh-adjacent to each other, even though they sit well within a huge (as-the-crow-flies)
    ///     leash/detection radius. Island A holds the monster's own home; island B holds the target's exact
    ///     position (<c>(100, _, 100)</c>) -- the arc-offset approach point collapses exactly onto that same
    ///     point whenever the lateral roll is 0 (a <see cref="ScriptedRandomSource" />-always-0 fixture), so no
    ///     genuine graph disconnection subtlety is needed beyond "the goal triangle is unreachable from the
    ///     start triangle."
    /// </summary>
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
        // Contract's own Trigger: "that scan is only ever attempted when the table is currently empty." Starts
        // the monster in Spawning (not Decision) so the Enter-draining tick below never itself dispatches
        // RunDecision, keeping DetectionThrottleTicks at a genuinely fresh 0 by the time Decision is forced.
        // The monster's very FIRST Decision-state tick would normally be blocked outright by the ~1s/2-tick
        // throttle (S07_MyGame05.cpp:127-131) if the scan ran at all (0+1=1 < 2) -- so engaging on that very
        // first tick proves the scan was skipped entirely, not merely that the throttle happened to also
        // elapse by coincidental timing.
        var (zone, monster) = CreateStandardZone(meleeRadius: 1000, leashRadius: 1000, runSpeed: 100,
            ai: new ScriptedRandomSource(0), startInSpawning: true);
        EnterPlayerAt(zone, 10, posX: 5, posZ: 0);
        zone.Tick(SimulationClock.LegacyTick); // drains the Enter; monster stays Spawning (FrameInfo1 = 100)

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5, 10, out var died, out _));
        Assert.False(died);

        monster.AiState = MonsterAiState.Decision; // force the transition, as if spawn-wait had just elapsed
        monster.StateTicks = 0;

        zone.Tick(SimulationClock.LegacyTick); // the monster's very FIRST Decision tick
        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.AttackWindup, live!.AiState); // in melee range (huge RadiusInfo1) -> immediate
        Assert.Equal(10, live.TargetCharacterId);
    }

    [Fact]
    public void EmptyTableAndFailedScan_NeverReachesEngagementStep_StaysIdle()
    {
        // Contract's own Trigger: when the table starts empty AND the scan itself finds nobody, the engagement
        // step never runs at all that tick -- distinct from the "no transition" outcome of an empty POST-prune
        // table. With no player anywhere nearby, the monster just stays put in Decision.
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
        // Contract side effect 2: pruning down to zero survivors is a hard stop -- no melee/chase/walk-home
        // transition of any kind, even though the table was non-empty (and the scan-skip gate was satisfied).
        var (zone, monster) = CreateStandardZone(meleeRadius: 1000, leashRadius: 1000, runSpeed: 100,
            ai: new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, posX: 5, posZ: 0);
        zone.Tick(SimulationClock.LegacyTick); // drains the Enter

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5, 10, out var died, out _));
        Assert.False(died);

        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.IsDead = true; // Prune's own dead-attacker exclusion drops this, the table's only entry

        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Decision, live!.AiState); // no transition -- still idle, not even ReturnToSpawn
        Assert.Null(live.TargetCharacterId);
    }

    [Fact]
    public void BeyondMeleeRangeSurvivor_NonPositiveChaseSpeed_NoTransitionAtAll()
    {
        // Contract's own edge case: beyond melee range but the monster's own configured chase speed is
        // non-positive -- distinct from the walk-home fallback; the monster simply takes no action this tick.
        var (zone, _) = CreateStandardZone(meleeRadius: 5, leashRadius: 1000, runSpeed: 0,
            ai: new ScriptedRandomSource(0));
        EnterPlayerAt(zone, 10, posX: 100, posZ: 0);
        // Tick 1: drains the Enter -- the monster is already in Decision (CreateStandardZone's own default), so
        // this SAME tick is also this monster's first Decision dispatch: DetectionThrottleTicks 0->1 (< 2,
        // throttle blocks). Tick 2: DetectionThrottleTicks 1->2 (no longer < 2) -> the scan runs and seeds this
        // same tick -> falls straight through to the engagement step, in the SAME tick, per the contract's own
        // Trigger.
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
        // Contract side effect 6: the approach point sits at the SAME distance from the monster as the real
        // target, but rotated off the straight line by a lateral offset drawn from [0, meleeRadius] -- not a
        // straight line at the target's own coordinates. A non-zero scripted lateral/sign roll proves the
        // rotation actually happened (a lateral of exactly 0 would collapse to the straight line, indistinguishable
        // from a bug that ignored the offset entirely).
        var (zone, _) = CreateStandardZone(meleeRadius: 10, leashRadius: 1000, runSpeed: 100,
            ai: new ScriptedRandomSource(0 /* coin flip: succeed */, 0 /* fallback pick: mod 1 -> irrelevant */,
                5 /* lateral: 5 % (meleeRadius+1=11) = 5 */, 1 /* sign: 1 % 2 == 1 -> +1f */),
            geometry: FlatGeometry());
        EnterPlayerAt(zone, 10, posX: 100, posZ: 0);
        // Tick 1 (drains the Enter) also doubles as this monster's first Decision dispatch (throttle 0->1,
        // blocked, no rolls consumed). Tick 2 crosses the throttle (1->2) and runs BOTH the scan and the
        // engagement step in that same tick -- asserting immediately afterward is required, since one more
        // tick would already have RunChase overwrite the broadcast target location with the live target's own
        // position (RunChase re-derives it every tick while actively engaged).
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.Chase, live!.AiState);
        Assert.Equal(10, live.TargetCharacterId);

        // Same distance from the monster (at the origin) as the real target (100 units away).
        var approachDistance = MathF.Sqrt(live.TargetLocationX * live.TargetLocationX +
                                           live.TargetLocationZ * live.TargetLocationZ);
        Assert.True(MathF.Abs(approachDistance - 100f) < 0.01f,
            $"approach point should sit exactly 100 units from the monster, was {approachDistance}");

        // Genuinely rotated off the straight line (Z != 0), with the scripted +1 sign producing a positive Z.
        Assert.True(live.TargetLocationZ > 0.01f,
            $"approach point should be laterally offset off the X axis, was ({live.TargetLocationX},{live.TargetLocationZ})");
    }

    [Fact]
    public void BeyondMeleeRangeSurvivor_UnreachableApproachPoint_TransitionsToReturnToSpawn()
    {
        // Contract side effect 8: when no walkable path to the computed approach point exists, the monster
        // walks home instead of chasing. Two navmesh islands with no shared adjacency -- both on-mesh, but not
        // connected -- while the target is still well within the monster's own (huge) leash/detection radius
        // as the crow flies.
        var (zone, _) = CreateStandardZone(meleeRadius: 5, leashRadius: 2000, runSpeed: 100,
            ai: new ScriptedRandomSource(0), geometry: TwoIslandGeometry());
        EnterPlayerAt(zone, 10, posX: 100, posZ: 100);
        // Same two-tick shape as the sibling arc-offset test: tick 1 drains the Enter and also blocks the
        // throttle (0->1); tick 2 crosses it (1->2) and runs the scan + engagement in that same tick.
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var live));
        Assert.Equal(MonsterAiState.ReturnToSpawn, live!.AiState);
    }
}

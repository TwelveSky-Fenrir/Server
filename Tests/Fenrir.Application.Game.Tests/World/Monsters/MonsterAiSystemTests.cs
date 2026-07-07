using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the monster FSM end-to-end: spawn-wait, proximity aggro, pursuit, attack-windup timing, the
///     current-position-to-target-position chase give-up guard, and forced return-to-spawn.
///     <see cref="MonsterSpawnScheduler" /> and <see cref="MonsterAiSystem" /> run together so these tests
///     exercise the real spawn -&gt; live -&gt; AI pipeline, not a hand-injected monster.
/// </summary>
/// <remarks>
///     A generously large <see cref="GameServerOptions.AoiCellSize" /> avoids spurious detection flakiness: spawn
///     scatter could otherwise occasionally push a monster's home into a different AOI cell than the test target.
/// </remarks>
public class MonsterAiSystemTests
{
    private static WorldDataCache CacheWithOneRegion(short frameInfo1, short frameInfo3, short radiusInfo1,
        short radiusInfo2, short walkSpeed, short runSpeed, float regionRadius, byte attackType = 1)
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
            RadiusInfo1 = radiusInfo1,
            RadiusInfo2 = radiusInfo2,
            WalkSpeed = walkSpeed,
            RunSpeed = runSpeed,
            // proactive aggro is gated to AttackType in {1,3,6} (SelectAvatarIndexForPossibleAttack, S07_MyGame05.cpp)
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
        // Detection now includes a 50% per-candidate coin flip (SelectAvatarIndexForPossibleAttack,
        // S07_MyGame05.cpp:208-213) -- ScriptedRandomSource(0) always resolves it to a guaranteed success so
        // these tests stay deterministic instead of flaking on an unlucky flip.
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0));
        var options = new GameServerOptions { AoiCellSize = 100_000f };
        return ZoneTestKit.CreateZone(1, options, simulationSystems: [scheduler, ai], worldData: cache);
    }

    [Fact]
    public void Monster_StaysInSpawningState_UntilFrameInfo1TicksElapse()
    {
        var zone = CreateZone(CacheWithOneRegion(3, 1, 0, 0,
            0, 0, 0));

        zone.Tick(SimulationClock.LegacyTick); // tick 1: spawns, StateTicks 0 -> 1
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(MonsterAiState.Spawning, monster!.AiState);

        zone.Tick(SimulationClock.LegacyTick); // tick 2: StateTicks 1 -> 2
        Assert.Equal(MonsterAiState.Spawning, monster.AiState);

        zone.Tick(SimulationClock.LegacyTick); // tick 3: StateTicks 2 -> 3 >= 3 -> Decision
        Assert.Equal(MonsterAiState.Decision, monster.AiState);
    }

    [Fact]
    public void Monster_WithNoNearbyPlayer_StaysIdleAtHome()
    {
        var zone = CreateZone(CacheWithOneRegion(1, 1, 50, 5,
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
        // AttackType=2 (a real, common value) must never trigger detection, even at point-blank range
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
        // detection uses RadiusInfo2; the attack-range transition inside Chase uses RadiusInfo1 (kept small
        // so the monster must close the gap first)
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
        // Attack range covers the whole map (radiusInfo2=1000) so the monster attacks in place, no chase movement.
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 10, posZ: 0)));

        zone.Tick(SimulationClock.LegacyTick); // spawn (FrameInfo1=1 -> Decision already this same tick's next pass)
        zone.Tick(SimulationClock
            .LegacyTick); // Decision: 1-second detection throttle blocks the very first check (mCheckDetectEnemyTime, S07_MyGame05.cpp:127-131) -- stays Decision
        zone.Tick(SimulationClock.LegacyTick); // Decision: throttle window elapsed -> detects -> Chase
        zone.Tick(SimulationClock.LegacyTick); // Chase: already in range -> AttackWindup, StateTicks=0
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(MonsterAiState.AttackWindup, monster!.AiState);

        zone.Tick(SimulationClock.LegacyTick); // StateTicks 0->1, still < 2
        Assert.Equal(MonsterAiState.AttackWindup, monster.AiState);

        zone.Tick(SimulationClock.LegacyTick); // StateTicks 1->2 >= 2 -> Decision
        Assert.Equal(MonsterAiState.Decision, monster.AiState);
    }

    [Fact]
    public void Monster_TargetEscapesDetectionRadiusMidChase_GivesUpToIdleThenEventuallyReturnsHome()
    {
        // Give-up guard (S07_MyGame05.cpp:1359-1366; identical basis in AdjustValidAttackTarget, :387-391):
        // squared distance from the monster's OWN CURRENT position to the TARGET's CURRENT position, against
        // the monster's detection radius (RadiusInfo2) -- NOT a distance-from-home leash. radiusInfo1 (melee)
        // is kept small so the very first Chase-state tick, before the target moves, does not immediately
        // transition straight to AttackWindup.
        var zone = CreateZone(CacheWithOneRegion(1, 1, 2, 50,
            10, 1000, 50));
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 20, posZ: 0)));

        zone.Tick(SimulationClock.LegacyTick); // spawn (FrameInfo1=1 -> Decision already this same tick's next pass)
        // spawn scatter means "home" is not necessarily (0,0,0) -- read it once the monster exists
        Assert.True(zone.TryGetMonster(1, out var spawned));
        var homeX = spawned!.HomeX;
        var homeZ = spawned.HomeZ;

        zone.Tick(SimulationClock
            .LegacyTick); // Decision: 1-second detection throttle blocks the very first check -- stays Decision
        zone.Tick(SimulationClock.LegacyTick); // Decision: throttle window elapsed -> detects -> Chase
        Assert.True(zone.TryGetMonster(1, out var chasing));
        Assert.Equal(MonsterAiState.Chase, chasing!.AiState);

        // Target outruns/teleports far beyond the monster's detection radius while the monster is already
        // committed to the chase -- per the behavior contract's inference, the only practical way this
        // current-position-to-target-position check trips mid-chase (a monster is always closing the gap
        // during a normal, successful chase, so it never trips on its own).
        Assert.True(zone.TryGetPlayer(10, out var target));
        target!.PosX = 5000f;
        target.PosZ = 0f;

        // monster-ai-aggro-pathing (2026-07 round): A005 routes a lost/out-of-range chase target back to the
        // idle/decision state (S07_MyGame05.cpp:1361 sets aSort=1), NOT straight to return-to-spawn -- the
        // monster gets an immediate same-tick-family re-detection attempt, then a rolling 60-second grace
        // period (MonsterAiSystem.RunDecision) before it ever actually heads home.
        zone.Tick(SimulationClock.LegacyTick); // Chase: target now far outside RadiusInfo2 -> give up -> Decision
        Assert.True(zone.TryGetMonster(1, out var gaveUp));
        Assert.Equal(MonsterAiState.Decision, gaveUp!.AiState);
        Assert.Null(gaveUp.TargetCharacterId);

        // arrival is an epsilon check in production (MonsterAiSystem.ArrivalEpsilon) -- exact equality would be flaky
        const float arrivalEpsilon = 1f;

        // No other player is ever back in range, so every idle tick's re-detection attempt keeps failing --
        // the monster loiters (idle, and possibly a short random-wander detour) for up to the full 60-second
        // grace period (120 legacy ticks, SimulationClock.MonsterIdleReturnHomeLegacyTicks) before it commits
        // to ReturnToSpawn, plus that state's own fixed animation-frame delay before it actually teleports
        // home -- a generous bound comfortably covers the grace period, a possible wander detour, and the
        // return-to-spawn delay.
        var monster = gaveUp;
        var returnedHome = false;
        for (var i = 0; i < 200 && !returnedHome; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out monster));
            if (monster!.AiState == MonsterAiState.Spawning &&
                MathF.Abs(monster.PosX - homeX) <= arrivalEpsilon &&
                MathF.Abs(monster.PosZ - homeZ) <= arrivalEpsilon)
                returnedHome = true;
        }

        Assert.True(returnedHome,
            "monster never gave up chasing a target that escaped its detection radius and returned home");
        Assert.True(MathF.Abs(monster!.PosX - homeX) <= arrivalEpsilon);
        Assert.True(MathF.Abs(monster.PosZ - homeZ) <= arrivalEpsilon);
    }

    [Fact]
    public void Monster_LosesTarget_ReacquiresNearbyReplacementDuringGracePeriod_InsteadOfHeadingHome()
    {
        // monster-ai-aggro-pathing (2026-07 round): the whole point of routing a lost chase target back to
        // Decision instead of straight to ReturnToSpawn is that the very next idle tick re-attempts the same
        // proximity scan used for original aggro -- a second player standing right next to the monster must
        // be re-engageable immediately, not only after a 60-second wait.
        //
        // Only ONE candidate is ever a live option at either acquisition point (the replacement doesn't enter
        // the zone until after the original is already locked as the chase target, and the original is moved
        // far away in the same beat the replacement arrives) so this test never depends on unspecified
        // same-cell iteration order between two simultaneously-eligible candidates.
        var zone = CreateZone(CacheWithOneRegion(1, 1, 2, 50,
            10, 1000, 50));
        var (originalSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(originalSession, 1, "Original", 20, posZ: 0)));

        zone.Tick(SimulationClock.LegacyTick); // spawn (FrameInfo1=1 -> Decision already this same tick's next pass)
        zone.Tick(SimulationClock
            .LegacyTick); // Decision: 1-second detection throttle blocks the very first check -- stays Decision
        zone.Tick(SimulationClock
            .LegacyTick); // Decision: throttle window elapsed -> detects the sole candidate (original) -> Chase

        Assert.True(zone.TryGetMonster(1, out var chasing));
        Assert.Equal(MonsterAiState.Chase, chasing!.AiState);
        Assert.Equal(10, chasing.TargetCharacterId);

        // Original target escapes far out of detection range; a replacement enters right next to the monster
        // in that same beat, so it's the sole candidate by the time the next idle re-detection scan runs.
        Assert.True(zone.TryGetPlayer(10, out var original));
        original!.PosX = 5000f;
        original.PosZ = 0f;
        var (replacementSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(replacementSession, 1, "Replacement", 5, posZ: 0)));

        zone.Tick(SimulationClock
            .LegacyTick); // drains the replacement's Enter, then Chase: original outside RadiusInfo2 -> give up -> Decision
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

    /// <summary>
    ///     Forces the spawn scheduler's random scatter radius to exactly 0 (<c>NextDouble</c> always 0) -- every monster
    ///     in these tests spawns deterministically AT its region's own location, never offset.
    /// </summary>
    private sealed class ZeroScatterRandom : Random
    {
        public override double NextDouble()
        {
            return 0;
        }
    }
}

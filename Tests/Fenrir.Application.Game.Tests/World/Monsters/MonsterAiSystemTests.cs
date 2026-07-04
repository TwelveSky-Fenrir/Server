using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Monsters;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the simplified monster FSM (report 05 §3) end-to-end: spawn-wait, proximity aggro, pursuit,
///     attack-windup timing, leash, and forced return-to-spawn. <see cref="MonsterSpawnScheduler" /> and
///     <see cref="MonsterAiSystem" /> run TOGETHER (as they would in production, via <see cref="ZoneRegistry" />)
///     so these tests exercise the real spawn -&gt; live -&gt; AI pipeline, not a hand-injected monster.
/// </summary>
/// <remarks>
///     A generously large <see cref="GameServerOptions.AoiCellSize" /> is used for every test that needs a
///     non-zero spawn-region radius: <see cref="Monsters.MonsterSpawnScheduler" /> scatters the monster's
///     actual home point randomly within that radius (report 05 §1), so with the default 75-unit AOI cell a
///     large-enough scatter can occasionally push the monster's home into a DIFFERENT AOI cell than a
///     fixed-position test target, making detection flaky. One huge cell sidesteps that without weakening
///     anything this suite actually asserts (detection here is deliberately tested via
///     <c>RadiusInfo1</c>/leash values, not the AOI partitioning itself, which <c>AoiGridTests</c> covers
///     separately).
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
            // Proactive aggro is gated to AttackType ∈ {1,3,6} (MonsterAiSystem.TryAcquireTarget's own remarks,
            // verified against SelectAvatarIndexForPossibleAttack, S07_MyGame05.cpp:123) -- every test in this
            // suite exercises the detect/chase/attack pipeline, so it defaults to a qualifying type; the
            // default (0, WorldDataTestRows.Monster's own baseline) would never detect anything -- see
            // Monster_NonAggressiveAttackType_NeverDetectsEvenAnAdjacentPlayer for the opposite case.
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
        var ai = new MonsterAiSystem();
        // See class remarks: one huge AOI cell removes spawn-scatter-vs-AOI-cell flakiness from these tests.
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
        // SelectAvatarIndexForPossibleAttack (S07_MyGame05.cpp:123): proactive aggro is gated to
        // mAttackType ∈ {1,3,6} -- a prior pass here had NO such gate at all, so every monster (regardless of
        // type) hunted any player within its detection radius. AttackType=2 (a real, common value -- e.g. the
        // Kobold seed data cited in review) must NEVER trigger detection, even for a player standing right on
        // top of the monster with a huge detection radius.
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
        // Detection uses RadiusInfo2 (must comfortably exceed the target's distance, 10, or the monster never
        // notices it at all); the attack-range transition inside Chase uses RadiusInfo1 (kept small so the
        // monster actually has to close the gap first, exercising the chase step, not just an instant attack).
        // regionRadius (=leash) must comfortably exceed the target's distance (10) so the leash never
        // interrupts the chase before the monster is close enough to attack -- see LeashRadius's own remarks.
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
        zone.Tick(SimulationClock.LegacyTick); // Decision detects -> Chase
        zone.Tick(SimulationClock.LegacyTick); // Chase: already in range -> AttackWindup, StateTicks=0
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(MonsterAiState.AttackWindup, monster!.AiState);

        zone.Tick(SimulationClock.LegacyTick); // StateTicks 0->1, still < 2
        Assert.Equal(MonsterAiState.AttackWindup, monster.AiState);

        zone.Tick(SimulationClock.LegacyTick); // StateTicks 1->2 >= 2 -> Decision
        Assert.Equal(MonsterAiState.Decision, monster.AiState);
    }

    [Fact]
    public void Monster_ChasingFarBeyondItsLeash_GivesUpAndReturnsHome()
    {
        // Detection (RadiusInfo2) must reach the far target (distance 500) for the monster to ever start
        // chasing it in the first place; the small attack-range (RadiusInfo1) is irrelevant here since the
        // leash (region radius, 50) gives up the chase long before the monster could ever close to it.
        var zone = CreateZone(CacheWithOneRegion(1, 1, 5, 1000,
            10, 1000, 50)); // leash = region radius = 50
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "FarTarget", 500, posZ: 0)));

        // The spawn scatter (report 05 §1) means "home" is NOT necessarily (0,0,0) -- read it once the
        // monster exists rather than assuming the region's own nominal location.
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var spawned));
        var homeX = spawned!.HomeX;
        var homeZ = spawned.HomeZ;

        // Arrival is an EPSILON check in production (MonsterAiSystem.ArrivalEpsilon = 1f, matching a legitimate
        // partial final step whose remaining distance rounds to just over/under the step length) -- exact
        // float equality against home is too strict here and would be flaky by construction.
        const float arrivalEpsilon = 1f;

        var monster = spawned;
        var returnedHome = false;
        for (var i = 0; i < 10 && !returnedHome; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out monster));
            if (monster!.AiState == MonsterAiState.Spawning &&
                MathF.Abs(monster.PosX - homeX) <= arrivalEpsilon &&
                MathF.Abs(monster.PosZ - homeZ) <= arrivalEpsilon)
                returnedHome = true;
        }

        Assert.True(returnedHome, "monster never gave up chasing an out-of-leash target and returned home");
        Assert.True(MathF.Abs(monster!.PosX - homeX) <= arrivalEpsilon);
        Assert.True(MathF.Abs(monster.PosZ - homeZ) <= arrivalEpsilon);
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

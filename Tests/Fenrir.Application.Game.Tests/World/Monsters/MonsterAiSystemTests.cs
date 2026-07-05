using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Monsters;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the monster FSM end-to-end: spawn-wait, proximity aggro, pursuit, attack-windup timing, leash, and
///     forced return-to-spawn. <see cref="MonsterSpawnScheduler" /> and <see cref="MonsterAiSystem" /> run together
///     so these tests exercise the real spawn -&gt; live -&gt; AI pipeline, not a hand-injected monster.
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
        var ai = new MonsterAiSystem();
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
        // so the monster must close the gap first); regionRadius (leash) must exceed target distance too
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
        // detection (RadiusInfo2) must reach the far target (500) so the leash (50) gives up before it ever closes in
        var zone = CreateZone(CacheWithOneRegion(1, 1, 5, 1000,
            10, 1000, 50)); // leash = region radius = 50
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "FarTarget", 500, posZ: 0)));

        // spawn scatter means "home" is not necessarily (0,0,0) -- read it once the monster exists
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var spawned));
        var homeX = spawned!.HomeX;
        var homeZ = spawned.HomeZ;

        // arrival is an epsilon check in production (MonsterAiSystem.ArrivalEpsilon) -- exact equality would be flaky
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

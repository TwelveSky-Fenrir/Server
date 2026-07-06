using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the two AI states added for cluster C15: <see cref="MonsterAiState.RangedAttackWindup" /> (the
///     Zone175-type-boss ranged opening <see cref="MonsterAiSystem.RunChase" /> can take instead of always
///     closing to melee range) and <see cref="MonsterAiState.Flinch" /> (hit-stagger tick-countdown).
/// </summary>
public class MonsterAiSystemBossAndFlinchTests
{
    private static WorldDataCache CacheWithOneRegion(byte specialType, short radiusInfo1, short radiusInfo2,
        short frameInfo4 = 2)
    {
        var monster = WorldDataTestRows.Monster(600) with
        {
            SpecialType = specialType,
            Life = 1000,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo2 = 3,
            FrameInfo4 = frameInfo4,
            RadiusInfo1 = radiusInfo1,
            RadiusInfo2 = radiusInfo2,
            WalkSpeed = 10,
            RunSpeed = 1000,
            AttackType = 1
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 600) with
        {
            Number = 1,
            LocationX = 0,
            LocationY = 0,
            LocationZ = 0,
            Radius = 50 // also the leash bound (MonsterEntity.LeashRadius) -- must clear the chase distance below
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
    public void Zone175TypeBoss_OutOfMeleeRangeButWithinDetectionRadius_OpensWithARangedAttack()
    {
        // Melee radius (RadiusInfo1=2) is far smaller than the target's actual distance (10); a non-boss
        // monster would have to keep closing in. RadiusInfo2=1000 easily covers it.
        var zone = CreateZone(CacheWithOneRegion(40, 2, 1000));
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 10, posZ: 0)));

        var reachedRangedWindup = false;
        for (var i = 0; i < 10 && !reachedRangedWindup; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            if (monster!.AiState == MonsterAiState.RangedAttackWindup)
                reachedRangedWindup = true;
        }

        Assert.True(reachedRangedWindup, "Zone175-type boss never opened with a ranged attack");
    }

    [Fact]
    public void NonBossMonster_SamePlacement_MustCloseToMeleeRange_NeverEntersRangedAttackWindup()
    {
        var zone = CreateZone(CacheWithOneRegion(0, 2, 1000));
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 10, posZ: 0)));

        var reachedMeleeWindup = false;
        for (var i = 0; i < 10 && !reachedMeleeWindup; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            Assert.NotEqual(MonsterAiState.RangedAttackWindup, monster!.AiState);
            if (monster.AiState == MonsterAiState.AttackWindup)
                reachedMeleeWindup = true;
        }

        Assert.True(reachedMeleeWindup, "non-boss monster never closed the distance to a normal melee attack");
    }

    [Fact]
    public void RangedAttackWindup_ReturnsToDecision_AfterFrameInfo4Ticks()
    {
        var zone = CreateZone(CacheWithOneRegion(40, 2, 1000, 2));
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 10, posZ: 0)));

        while (true)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            if (monster!.AiState == MonsterAiState.RangedAttackWindup)
                break;
        }

        Assert.True(zone.TryGetMonster(1, out var windingUp));
        Assert.Equal(0, windingUp!.StateTicks);

        zone.Tick(SimulationClock.LegacyTick); // StateTicks 0->1, still < 2
        Assert.True(zone.TryGetMonster(1, out var stillWindingUp));
        Assert.Equal(MonsterAiState.RangedAttackWindup, stillWindingUp!.AiState);

        zone.Tick(SimulationClock.LegacyTick); // StateTicks 1->2 >= 2 -> Decision
        Assert.True(zone.TryGetMonster(1, out var afterWindup));
        Assert.Equal(MonsterAiState.Decision, afterWindup!.AiState);
    }

    [Fact]
    public void Flinch_ReturnsToDecision_AfterFrameInfo2Ticks()
    {
        var zone = CreateZone(CacheWithOneRegion(0, 0, 0));
        zone.Tick(SimulationClock.LegacyTick); // pop + Spawning -> Decision (FrameInfo1=1)
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(MonsterAiState.Decision, monster!.AiState);

        // Directly drive the monster into Flinch, as if some (not-yet-wired) big-hit trigger had just fired.
        monster.AiState = MonsterAiState.Flinch;
        monster.StateTicks = 0;

        zone.Tick(SimulationClock.LegacyTick); // StateTicks 0->1, still < FrameInfo2=3
        Assert.Equal(MonsterAiState.Flinch, monster.AiState);

        zone.Tick(SimulationClock.LegacyTick); // StateTicks 1->2, still < 3
        Assert.Equal(MonsterAiState.Flinch, monster.AiState);

        zone.Tick(SimulationClock.LegacyTick); // StateTicks 2->3 >= 3 -> Decision
        Assert.Equal(MonsterAiState.Decision, monster.AiState);
        Assert.Equal(0, monster.StateTicks);
    }

    /// <summary>Forces the spawn scheduler's scatter radius to exactly 0 (same helper as <see cref="MonsterAiSystemTests" />).</summary>
    private sealed class ZeroScatterRandom : Random
    {
        public override double NextDouble()
        {
            return 0;
        }
    }
}

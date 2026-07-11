using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers workstream A3 (behavior contract <c>A3-ai-recipes</c>): the <c>mSpecialSortNumber</c> archetype
///     selector derivation (<see cref="MonsterSpecialSort" />), the car-thrower/ranged annulus recipe, the
///     Zone175-type boss multi-candidate acquisition, and the <c>AdjustValidAttackTarget</c> mid-chase target
///     prune. Like the sibling monster-AI tests, everything is observed through <see cref="Zone" />'s public
///     surface only (<see cref="Zone.TryDamageMonster" />/<see cref="Zone.TryDequeueDeadMonster" /> are the only
///     externally-visible witnesses of the internal shared attacker table).
/// </summary>
public class MonsterAiSystemRecipesTests
{
    // ---- item 1: mSpecialSortNumber derivation -------------------------------------------------------------

    [Fact]
    public void SpecialSort_OrdinaryMonster_DerivesToStandard()
    {
        // The dominant path: any (Type, SpecialType) with no grounded special mapping falls to the standard-mob
        // default (contract's "1 (standard mob) -- the dominant path for nearly all monsters").
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(1, 1));
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(2, 7));

        // Zone175-boss special types (40-44) are router-gated on SpecialType directly, so their archetype
        // derivation is never consulted and is left at the standard default here.
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(1, 40));
        Assert.Equal(MonsterSpecialSort.Standard, MonsterSpecialSort.Derive(1, 44));
    }

    [Theory]
    [InlineData((byte)11)]
    [InlineData((byte)12)]
    [InlineData((byte)13)]
    [InlineData((byte)28)]
    [InlineData((byte)14)]
    [InlineData((byte)15)] // recovered 2026-07-11: a sixth Tribe-Symbol-Stone code, a genuine no-op one level
    // deeper inside the recipe body itself, but still resolves to this archetype at the selector level.
    public void SpecialSort_TribeSymbolSpecialTypes_DeriveToTribeSymbolStone(byte specialType)
    {
        // The one non-default mapping with an independent Fenrir anchor (MonsterSpawnScheduler's own
        // 11/12/13/28/14 Holy-Stone symbol map, plus the recovered 15). Type is unused for this branch --
        // Type 2 stands in for "any Type outside the Tribe-Guard set {6,7,8,9}" (Type 9 would no longer prove
        // the same point: recovered 2026-07-11, Type 9 unconditionally derives TribeGuard regardless of
        // SpecialType -- see SpecialSort_TribeGuardTypes_DeriveToTribeGuard_RegardlessOfSpecialType below).
        Assert.Equal(MonsterSpecialSort.TribeSymbolStone, MonsterSpecialSort.Derive(1, specialType));
        Assert.Equal(MonsterSpecialSort.TribeSymbolStone, MonsterSpecialSort.Derive(2, specialType));
    }

    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    [InlineData((byte)8)]
    [InlineData((byte)9)]
    public void SpecialSort_TribeGuardTypes_DeriveToTribeGuard_RegardlessOfSpecialType(byte type)
    {
        // Recovered 2026-07-11: Type 6/7/8/9 -> Tribe Guard unconditionally, closing MonsterAiSystem's own
        // previously-documented "Derive has no discriminator entry mapping a guard's (Type, SpecialType) to
        // TribeGuard" gap. SpecialType is not inspected at all once Type matches -- proven here by using 11,
        // a value that WOULD otherwise resolve to TribeSymbolStone under Type==1, to show Type dominates.
        Assert.Equal(MonsterSpecialSort.TribeGuard, MonsterSpecialSort.Derive(type, 11));
        Assert.Equal(MonsterSpecialSort.TribeGuard, MonsterSpecialSort.Derive(type, 0));
    }

    [Fact]
    public void SpecialSort_IsSeededOntoTheEntityAtSpawn()
    {
        var template = WorldDataTestRows.Monster(600) with { Type = 1, SpecialType = 12 };
        var entity = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0, 50);
        Assert.Equal(MonsterSpecialSort.TribeSymbolStone, entity.SpecialSort);

        // The explicit override is honoured (the seam recipes whose live discriminator isn't grounded yet use).
        var overridden = MonsterEntity.Create(2, 2, template, 1, 0, 0, 0, 50,
            specialSort: MonsterSpecialSort.CarThrower);
        Assert.Equal(MonsterSpecialSort.CarThrower, overridden.SpecialSort);
    }

    // ---- items 3+4: car-thrower / ranged annulus hit-test --------------------------------------------------

    [Fact]
    public void ThrowCar_TargetInsideTheAnnulusBand_CommitsMeleeAttack()
    {
        // Ranged hit-test lands a hit: melee radius (RadiusInfo1) = 100, so the annulus band is [25, 100] on
        // the true 3D length. A player at 3D distance 50 sits squarely inside it -> the thrower commits to the
        // melee-attack sort (which resolves the hit through Zone.ResolveMonsterAttack on its own windup).
        var (zone, monster) = CreateZoneWithManualMonster(meleeRadius: 100, ai: new ScriptedRandomSource(5, 0));
        EnterPlayerAt(zone, 10, posX: 50, posZ: 0);

        zone.Tick(SimulationClock.LegacyTick); // spawn-wait -> Decision (FrameInfo1 == 1)
        Assert.Equal(MonsterAiState.Decision, monster.AiState);

        zone.Tick(SimulationClock.LegacyTick); // Decision: wander roll (5, no wander) -> annulus hit -> AttackWindup
        Assert.Equal(MonsterAiState.AttackWindup, monster.AiState);
        Assert.Equal(10, monster.TargetCharacterId);
    }

    [Fact]
    public void ThrowCar_TargetInsideTheInnerDeadZone_IsNotAcquired()
    {
        // The annulus REJECTS a target closer than 25% of the melee radius (the thrower can't lob point-blank):
        // radius 100 -> inner band 25, a player at 3D distance 10 is inside the dead zone and never acquired.
        // The single-value random source keeps the ~1% wander roll (NextInt32(100) == 5) from ever firing, so
        // the monster simply idles every tick rather than occasionally wandering.
        var (zone, monster) = CreateZoneWithManualMonster(meleeRadius: 100, ai: new ScriptedRandomSource(5));
        EnterPlayerAt(zone, 10, posX: 10, posZ: 0);

        for (var i = 0; i < 5; i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.NotEqual(MonsterAiState.AttackWindup, monster.AiState);
        Assert.Null(monster.TargetCharacterId);
    }

    [Fact]
    public void ThrowCar_WanderTrigger_EntersPatrol()
    {
        // With the ~1% wander roll forced to hit (ScriptedRandomSource(0)) the thrower wanders instead of
        // attacking -- picks a random nearby destination clearing the 50-unit minimum and enters the patrol
        // sort. No player is needed: the wander trigger is a self-roll.
        var (zone, monster) = CreateZoneWithManualMonster(meleeRadius: 100, ai: new ScriptedRandomSource(0));

        zone.Tick(SimulationClock.LegacyTick); // spawn-wait -> Decision
        zone.Tick(SimulationClock.LegacyTick); // Decision: wander roll (0) hits -> Patrol

        Assert.Equal(MonsterAiState.Patrol, monster.AiState);
    }

    // ---- item 5: Zone175-type boss multi-candidate acquisition --------------------------------------------

    [Fact]
    public void Zone175Boss_MultiCandidate_WritesEveryInRangeCandidateToTheSharedTable()
    {
        // The distinguishing property of the multi-candidate acquisition vs. the single-target standard scan:
        // it records EVERY 50%-accepted in-range candidate into the shared attacker table, not just the one it
        // ends up committing as its locked target. Two players are both in the wide radius; after acquisition
        // the boss has ONE locked target, but the OTHER (never the boss's committed target) still received an
        // acquisition slot -- proven by giving that non-locked player real damage and watching kill credit land
        // on it (impossible in a single-target model, where the non-locked player would never be in the table).
        var zone = CreateBossZone();
        EnterPlayerAt(zone, 10, posX: 5, posZ: 0); // dist^2 = 25, both outside melee (RadiusInfo1 = 2 -> 4)
        EnterPlayerAt(zone, 11, posX: 8, posZ: 0); // dist^2 = 64, both inside the wide radius (RadiusInfo2 = 1000)

        var boss = AcquireBossTarget(zone);
        var lockedId = boss.TargetCharacterId!.Value;
        var nonLockedId = lockedId == 10 ? 11 : 10;

        // Real damage to the NON-locked candidate only -- it is in the table solely because the multi-candidate
        // acquisition wrote it there.
        Assert.True(zone.TryGetMonster(boss.ServerIndex, out var live));
        Assert.True(zone.TryDamageMonster(live!.ServerIndex, 500, nonLockedId, out var died, out var remaining));
        Assert.False(died);

        // Finishing blow bypasses damage tracking (null attacker); kill credit is the max-damage tracked entry.
        Assert.True(zone.TryDamageMonster(live.ServerIndex, remaining, null, out var killed, out _));
        Assert.True(killed);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(nonLockedId, deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void Zone175Boss_NoNearbyPlayer_StaysIdle()
    {
        // With nobody in range the multi-candidate acquisition records nobody, so the boss takes no action.
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

    // ---- item 2: AdjustValidAttackTarget mid-chase prune -------------------------------------------------

    [Fact]
    public void AdjustValidAttackTarget_DropsAMidCrossShardTransferTarget()
    {
        // A standard monster locked in Chase must drop a target that has entered a cross-shard transfer
        // (IsMovingZone) -- the gauntlet check the pre-A3 chase give-up did NOT apply -- routing back to the
        // idle/decision state, not straight home.
        var zone = CreateStandardChaseZone();
        EnterPlayerAt(zone, 10, posX: 20, posZ: 0);

        var monster = AcquireChaseTarget(zone, 10);

        Assert.True(zone.TryGetPlayer(10, out var target));
        target!.IsMovingZone = true;

        zone.Tick(SimulationClock.LegacyTick); // RunChase -> AdjustValidAttackTarget prunes -> Decision
        Assert.True(zone.TryGetMonster(1, out var pruned));
        Assert.Equal(MonsterAiState.Decision, pruned!.AiState);
        Assert.Null(pruned.TargetCharacterId);
    }

    // ---- fixtures ----------------------------------------------------------------------------------------

    /// <summary>
    ///     A zone with just the AI system (no spawn scheduler) and one manually-spawned car-thrower
    ///     (<see cref="MonsterSpecialSort.CarThrower" /> via the <see cref="MonsterEntity.Create" /> override,
    ///     since no live derivation reaches sort 6 until the discriminator table is reopened). At (0,0,0),
    ///     <c>FrameInfo1 = 1</c> so one tick reaches the decision sort.
    /// </summary>
    private static (Zone Zone, MonsterEntity Monster) CreateZoneWithManualMonster(short meleeRadius,
        IRandomSource ai)
    {
        var template = WorldDataTestRows.Monster(600) with
        {
            Life = 100_000,
            AttackType = 1, // thrower acquisition gate: AttackType in {1,3,6}
            RadiusInfo1 = meleeRadius, // annulus outer bound / inner dead-zone basis
            WalkSpeed = 10, // wander walk-speed gate
            RunSpeed = 100,
            FrameInfo1 = 1,
            FrameInfo3 = 2
        };
        var monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0, 50,
            specialSort: MonsterSpecialSort.CarThrower);

        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options, simulationSystems: [new MonsterAiSystem(ai)]);
        zone.SpawnMonster(monster);
        return (zone, monster);
    }

    private static Zone CreateBossZone()
    {
        var monster = WorldDataTestRows.Monster(600) with
        {
            SpecialType = 40, // Zone175-type boss band (40-44)
            Life = 1_000_000,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo4 = 2,
            RadiusInfo1 = 2, // small melee radius
            RadiusInfo2 = 1000, // wide detection radius
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
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0)); // all coin flips / branch rolls succeed
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
            RadiusInfo1 = 2, // small melee radius keeps the monster in Chase long enough to observe the prune
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

    /// <summary>Forces the spawn scheduler's scatter radius to exactly 0 (same helper as <see cref="MonsterAiSystemTests" />).</summary>
    private sealed class ZeroScatterRandom : Random
    {
        public override double NextDouble()
        {
            return 0;
        }
    }
}

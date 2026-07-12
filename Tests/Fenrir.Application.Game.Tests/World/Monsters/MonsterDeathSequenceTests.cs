using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterDeathSequenceTests
{
    private static MonsterEntity CreateEntity(byte damageType = 2, short frameInfo5 = 1)
    {
        var template = WorldDataTestRows.Monster(900) with { DamageType = damageType, FrameInfo5 = frameInfo5 };
        return MonsterEntity.Create(1, 1u, template, 1, 100f, 0f, 100f);
    }


    [Fact]
    public void Compute_StationaryDamageType_IsAlwaysZero_RegardlessOfDistanceOrCrit()
    {
        var random = new ScriptedRandomSource(2);

        var nonCritical = MonsterDeathKnockback.Compute(0, 0, 500, 500,
            MonsterDeathKnockback.StationaryDamageType, false, random);
        var critical = MonsterDeathKnockback.Compute(0, 0, 500, 500,
            MonsterDeathKnockback.StationaryDamageType, true, random);

        Assert.True(nonCritical.IsZero);
        Assert.True(critical.IsZero);
    }

    [Fact]
    public void Compute_PointBlankKill_IsZero()
    {
        var random = new ScriptedRandomSource(2);

        var atThreshold = MonsterDeathKnockback.Compute(0, 0, MonsterDeathKnockback.PointBlankDistanceThreshold, 0,
            2, false, random);
        var wellInside = MonsterDeathKnockback.Compute(0, 0, 0.1f, 0, 2, false, random);

        Assert.True(atThreshold.IsZero);
        Assert.True(wellInside.IsZero);
    }

    [Fact]
    public void Compute_Direction_PointsAwayFromKiller_TowardMonster()
    {
        var random = new ScriptedRandomSource(0);

        var knockback = MonsterDeathKnockback.Compute(0, 0, 3, 4, 2, false, random);

        Assert.Equal(0.6f, knockback.X, 3);
        Assert.Equal(0.8f, knockback.Z, 3);
    }

    [Fact]
    public void Compute_CriticalHit_IsAlwaysShort_AndConsumesNoRandomDraw()
    {
        var random = new CountingRandomSource(0);

        var knockback = MonsterDeathKnockback.Compute(0, 0, 10, 0, 2, true, random);

        Assert.Equal(MonsterDeathKnockback.ShortMagnitude, knockback.X, 3);
        Assert.Equal(0f, knockback.Z, 3);
        Assert.Equal(0, random.DrawCount);
    }

    [Theory]
    [InlineData(0, MonsterDeathKnockback.ShortMagnitude)]
    [InlineData(1, MonsterDeathKnockback.ShortMagnitude)]
    [InlineData(2, MonsterDeathKnockback.LongMagnitudeA)]
    [InlineData(3, MonsterDeathKnockback.LongMagnitudeB)]
    public void Compute_NonCritical_FourWayDraw_ProducesExpectedMagnitude(int scriptedDraw, float expectedMagnitude)
    {
        var random = new ScriptedRandomSource(scriptedDraw);

        var knockback = MonsterDeathKnockback.Compute(0, 0, 10, 0, 2, false, random);

        Assert.Equal(expectedMagnitude, knockback.X, 3);
        Assert.Equal(0f, knockback.Z, 3);
    }


    [Fact]
    public void BeginCorpseCountdown_TransitionsToDead_AndResetsStateTicks()
    {
        var monster = CreateEntity();
        monster.AiState = MonsterAiState.Chase;
        monster.StateTicks = 7;

        MonsterDeathSequence.BeginCorpseCountdown(monster, 90f, 100f, false, new ScriptedRandomSource(0));

        Assert.Equal(MonsterAiState.Dead, monster.AiState);
        Assert.Equal(0, monster.StateTicks);
    }

    [Fact]
    public void BeginCorpseCountdown_StoresKnockbackVectorIntoRepurposedTargetLocation_YForcedFlat()
    {
        var monster = CreateEntity();

        var knockback = MonsterDeathSequence.BeginCorpseCountdown(monster, 90f, 100f, false,
            new ScriptedRandomSource(0));

        Assert.Equal(knockback.X, monster.TargetLocationX);
        Assert.Equal(knockback.Z, monster.TargetLocationZ);
        Assert.Equal(0f, monster.TargetLocationY);
        Assert.Equal(1f, monster.TargetLocationX, 3);
        Assert.Equal(0f, monster.TargetLocationZ, 3);
    }

    [Fact]
    public void BeginCorpseCountdown_FacesTheCorpseTowardItsKiller()
    {
        var monster = CreateEntity();

        MonsterDeathSequence.BeginCorpseCountdown(monster, 90f, 100f, false, new ScriptedRandomSource(0));

        var expectedHeading = MathF.Atan2(90f - monster.PosX, 100f - monster.PosZ);
        Assert.Equal(expectedHeading, monster.Heading, 4);
    }

    [Fact]
    public void BeginCorpseCountdown_ReturnsTheSameVectorItStores()
    {
        var monster = CreateEntity();

        var returned = MonsterDeathSequence.BeginCorpseCountdown(monster, 90f, 100f, false,
            new ScriptedRandomSource(2));

        Assert.Equal(MonsterDeathKnockback.LongMagnitudeA, returned.X, 3);
        Assert.Equal(returned.X, monster.TargetLocationX);
        Assert.Equal(returned.Z, monster.TargetLocationZ);
    }


    [Fact]
    public void IsCorpseCountdownComplete_FalseBeforeThreshold_TrueAtOrAfter()
    {
        var monster = CreateEntity(frameInfo5: 3);

        monster.StateTicks = 0;
        Assert.False(MonsterDeathSequence.IsCorpseCountdownComplete(monster));

        monster.StateTicks = 2;
        Assert.False(MonsterDeathSequence.IsCorpseCountdownComplete(monster));

        monster.StateTicks = 3;
        Assert.True(MonsterDeathSequence.IsCorpseCountdownComplete(monster));

        monster.StateTicks = 4;
        Assert.True(MonsterDeathSequence.IsCorpseCountdownComplete(monster));
    }

    [Fact]
    public void IsCorpseCountdownComplete_DegenerateThreshold_FloorsToOneTick()
    {
        var monster = CreateEntity(frameInfo5: 0);

        monster.StateTicks = 0;
        Assert.False(MonsterDeathSequence.IsCorpseCountdownComplete(monster));

        monster.StateTicks = 1;
        Assert.True(MonsterDeathSequence.IsCorpseCountdownComplete(monster));
    }


    [Fact]
    public void ApplyPvmAttack_KillingBlow_AppliesKnockbackAndFacing_UsingTheAttackersOwnSenderLocation()
    {
        var monsterTemplate = WorldDataTestRows.Monster(901) with
        {
            Life = 10,
            DamageType = 2,
            ItemLevel = 1,
            RealLevel = 1,
            GeneralExperience = 10,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 901) with
        {
            Number = 1,
            LocationX = 100,
            LocationY = 0,
            LocationZ = 100,
            Radius = 0
        };
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [monsterTemplate], MonsterSpawnRegions = [region]
        };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        var scheduler = new MonsterSpawnScheduler(cache);
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache,
            randomSource: new ScriptedRandomSource(0));

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Attacker")));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var monster));
        monster!.AiState = MonsterAiState.Decision;

        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.Stats = new EffectiveStats(1000, 1000, 500, 0, 1000, 0, 0, 0, 0, 0, 0);
        attacker.AttackSubPacketCeiling = int.MaxValue;
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        const float killerX = 90f;
        const float killerZ = 100f;
        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = 10,
            AttackInfo = new AttackForProtocol
            {
                Case = 3,
                ServerIndex1 = 10,
                UniqueNumber1 = unchecked(10),
                ServerIndex2 = monster.ServerIndex,
                UniqueNumber2 = monster.UniqueNumber,
                SenderLocation = [killerX, 0, killerZ],
                AttackActionValue1 = 1,
                AttackActionValue2 = 0,
                AttackActionValue3 = 0,
                AttackActionValue4 = 0,
                AttackResultValue = 0,
                AttackCriticalExist = 0,
                AttackElementDamage = 0,
                AttackViewDamageValue = 0,
                AttackRealDamageValue = 0
            }
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);

        Assert.Equal(MonsterAiState.Dead, monster.AiState);
        Assert.Equal(0, monster.StateTicks);

        var expected = MonsterDeathKnockback.Compute(killerX, killerZ, 100f, 100f, 2, false,
            new ScriptedRandomSource(0));
        Assert.Equal(expected.X, monster.TargetLocationX, 3);
        Assert.Equal(expected.Z, monster.TargetLocationZ, 3);
        Assert.Equal(0f, monster.TargetLocationY);

        var expectedHeading = MathF.Atan2(killerX - 100f, killerZ - 100f);
        Assert.Equal(expectedHeading, monster.Heading, 4);
    }

    private sealed class CountingRandomSource(int scriptedValue) : IRandomSource
    {
        public int DrawCount { get; private set; }

        public int NextInt32(int exclusiveUpperBound)
        {
            DrawCount++;
            return scriptedValue % exclusiveUpperBound;
        }
    }
}

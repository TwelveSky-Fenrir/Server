using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Combat;

public class MonsterCombatResolverTribeSymbolMalusTests
{
    private const int AttackerAttackPower = 1000;

    private static MonsterEntity Monster()
    {
        var template = WorldDataTestRows.Monster(700) with
        {
            Life = 100_000,
            DefensePower = 0,
            AttackBlock = 0,
            ElementDefensePower = 0
        };

        return MonsterEntity.Create(1, 1u, template, 1, 0f, 0f, 0f);
    }

    private static CombatantSnapshot Attacker(short level)
    {
        var stats = new EffectiveStats(1000, 0, AttackerAttackPower, 0, 100, 0, 0, 0, 0, 0, 0);
        return new CombatantSnapshot(10, 0, false, 1000, 1000, 0f, 0f, 0f, null, stats, 0, level);
    }

    private static AttackForProtocol MeleeRequest(MonsterEntity monster)
    {
        return new AttackForProtocol
        {
            Case = 3,
            ServerIndex1 = 10,
            UniqueNumber1 = 10u,
            ServerIndex2 = monster.ServerIndex,
            UniqueNumber2 = monster.UniqueNumber,
            SenderLocation = [0, 0, 0],
            AttackActionValue1 = 1,
            AttackActionValue2 = 0,
            AttackActionValue3 = 0,
            AttackActionValue4 = 0,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
    }

    private static ScriptedRandomSource NoVarianceRng()
    {
        return new ScriptedRandomSource(0);
    }

    [Fact]
    public void LevelAboveThreshold_NonZeroPenalty_ReducesDamageByThePenaltyFraction()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(113), monster, MeleeRequest(monster),
            TimeSpan.Zero, null, NoVarianceRng(), false, 0,
            0,
            TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty);

        Assert.True(outcome.Hit);
        Assert.Equal(800, outcome.DamageApplied);
        Assert.Equal(800, outcome.ViewDamage);
    }

    [Fact]
    public void LevelAtExactlyTheThreshold_NotStrictlyAbove_MalusNeverApplied()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(MonsterCombatResolver.MalusMinimumAttackerLevel),
            monster, MeleeRequest(monster), TimeSpan.Zero, null, NoVarianceRng(), false,
            0, 0,
            TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty);

        Assert.True(outcome.Hit);
        Assert.Equal(AttackerAttackPower, outcome.DamageApplied);
    }

    [Fact]
    public void LevelAboveThreshold_ZeroPenalty_NoReduction()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(113), monster, MeleeRequest(monster),
            TimeSpan.Zero, null, NoVarianceRng(), false, 0,
            0, 0f);

        Assert.True(outcome.Hit);
        Assert.Equal(AttackerAttackPower, outcome.DamageApplied);
    }

    [Fact]
    public void DefaultParameter_OmittedEntirely_BehavesAsUnmalused_ExistingCallersUnaffected()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(200), monster, MeleeRequest(monster),
            TimeSpan.Zero, null, NoVarianceRng(), false, 0, 0);

        Assert.True(outcome.Hit);
        Assert.Equal(AttackerAttackPower, outcome.DamageApplied);
    }
}

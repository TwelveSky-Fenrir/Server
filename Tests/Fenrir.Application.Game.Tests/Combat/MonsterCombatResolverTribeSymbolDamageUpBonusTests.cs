using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers the <c>tribesymbol-damage-magnitude</c> contract's flat damage-up bonus term in
///     <see cref="MonsterCombatResolver.ResolvePvmAttack" /> -- the previously-unmodeled per-increment magnitude
///     (<see cref="MonsterCombatResolver.DamageUpBonusFlatPerIncrement" />, 500) is now actually applied to
///     damage here, multiplied by <see cref="TribeSymbolCombatModifiers.GetDamageUpBonusIncrementCount" />'s
///     result, immediately after the elemental term and before the (unrelated-tribe) damage-down malus. This is
///     a pure resolver-level test (no <c>Zone</c>); <see cref="TribeSymbolDamageUpBonusTests" /> covers the
///     increment-COUNT producer itself and deliberately never asserts on damage.
/// </summary>
public class MonsterCombatResolverTribeSymbolDamageUpBonusTests
{
    private const int AttackerAttackPower = 1000;

    private static MonsterEntity Monster()
    {
        var template = WorldDataTestRows.Monster(700) with
        {
            Life = 1_000_000,
            DefensePower = 0,
            AttackBlock = 0,
            ElementDefensePower = 0
        };

        return MonsterEntity.Create(1, 1u, template, 1, 0f, 0f, 0f, 100f);
    }

    private static CombatantSnapshot Attacker(short level = 1)
    {
        // MaxLife, MaxMana, AttackPower, DefensePower, AttackSuccess, AttackBlock, Critical, CriticalDefence,
        // Luck, ElementAttackPower, ElementDefensePower -- Critical=0 so RollCritical never even draws from
        // the rng (CombatMath.RollCritical short-circuits on a non-positive chance).
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
            AttackActionValue1 = 1, // melee -- no skill echo sub-check
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

    // Draw sequence wraps and reduces modulo the requested bound -- a single 0 satisfies both ApplyVariance
    // draws (add-vs-subtract, then 0% magnitude) with no variance change; Critical=0 above means RollCritical
    // never draws at all.
    private static ScriptedRandomSource NoVarianceRng()
    {
        return new ScriptedRandomSource(0);
    }

    [Theory]
    [InlineData(1, 1_500)]
    [InlineData(2, 2_000)]
    [InlineData(3, 2_500)]
    [InlineData(TribeSymbolCombatModifiers.MaxDamageUpBonusIncrementCount, 3_000)] // 4 increments -> +2000
    public void IncrementCountAboveZero_AddsFlatFiveHundredPerIncrement(int incrementCount, int expectedDamage)
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), attackerAttackBudgetEnforced: false, attackerActionSkillNumber: 0,
            attackerActionSkillGradePoints: 0, attackerSymbolDamageDownPenalty: 0f,
            attackerSymbolDamageUpBonusIncrementCount: incrementCount);

        Assert.True(outcome.Hit);
        // 1000 base damage + (500 * incrementCount).
        Assert.Equal(expectedDamage, outcome.DamageApplied);
        Assert.Equal(expectedDamage, outcome.ViewDamage);
    }

    [Fact]
    public void IncrementCountZero_NoAdditionAtAll()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), attackerAttackBudgetEnforced: false, attackerActionSkillNumber: 0,
            attackerActionSkillGradePoints: 0, attackerSymbolDamageDownPenalty: 0f,
            attackerSymbolDamageUpBonusIncrementCount: 0);

        Assert.True(outcome.Hit);
        Assert.Equal(AttackerAttackPower, outcome.DamageApplied);
    }

    [Fact]
    public void DefaultParameter_OmittedEntirely_BehavesAsNoBonus_ExistingCallersUnaffected()
    {
        // Every existing positional call site predating this contract omits the new trailing parameter
        // entirely -- must default to "no damage-up bonus".
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), false, 0, 0);

        Assert.True(outcome.Hit);
        Assert.Equal(AttackerAttackPower, outcome.DamageApplied);
    }

    [Fact]
    public void BonusCompoundsBeforeTheDamageDownMalus_MalusAppliesToTheBonusedTotal()
    {
        // Own-symbol-lost malus is a flat 20% -- verifies ordering: bonus first (1000 + 500 = 1500), THEN the
        // malus reduces the already-bonused total (1500 - 20% = 1200), matching the contract's own "compounds
        // after the flat bonus" ordering.
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(113), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), attackerAttackBudgetEnforced: false, attackerActionSkillNumber: 0,
            attackerActionSkillGradePoints: 0,
            attackerSymbolDamageDownPenalty: TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty,
            attackerSymbolDamageUpBonusIncrementCount: 1);

        Assert.True(outcome.Hit);
        Assert.Equal(1_200, outcome.DamageApplied);
    }

    [Fact]
    public void DamageUpBonusFlatPerIncrement_IsTheCitedFiveHundred()
    {
        Assert.Equal(500, MonsterCombatResolver.DamageUpBonusFlatPerIncrement);
    }
}

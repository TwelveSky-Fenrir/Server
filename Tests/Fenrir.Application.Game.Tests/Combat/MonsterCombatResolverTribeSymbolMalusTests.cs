using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers the B15 (wave15 contract) PvM tribe-symbol malus term in
///     <see cref="MonsterCombatResolver.ResolvePvmAttack" /> -- the previously-built, previously-unconsumed
///     <see cref="TribeSymbolCombatModifiers.GetDamageDownPenalty" /> is now actually applied to damage here,
///     gated on the attacker's own level strictly exceeding <see cref="MonsterCombatResolver.MalusMinimumAttackerLevel" />,
///     positioned after the elemental-damage term. This is a pure resolver-level test (no <c>Zone</c>); the
///     end-to-end wiring through <c>Zone.Combat.cs</c> is covered separately.
/// </summary>
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

        return MonsterEntity.Create(1, 1u, template, 1, 0f, 0f, 0f, 100f);
    }

    private static CombatantSnapshot Attacker(short level)
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

    [Fact]
    public void LevelAboveThreshold_NonZeroPenalty_ReducesDamageByThePenaltyFraction()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(113), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), attackerAttackBudgetEnforced: false, attackerActionSkillNumber: 0,
            attackerActionSkillGradePoints: 0,
            attackerSymbolDamageDownPenalty: TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty);

        Assert.True(outcome.Hit);
        // 1000 base damage - 20% (0.2f) = 800.
        Assert.Equal(800, outcome.DamageApplied);
        Assert.Equal(800, outcome.ViewDamage);
    }

    [Fact]
    public void LevelAtExactlyTheThreshold_NotStrictlyAbove_MalusNeverApplied()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(MonsterCombatResolver.MalusMinimumAttackerLevel),
            monster, MeleeRequest(monster), TimeSpan.Zero, NoVarianceRng(), attackerAttackBudgetEnforced: false,
            attackerActionSkillNumber: 0, attackerActionSkillGradePoints: 0,
            attackerSymbolDamageDownPenalty: TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty);

        Assert.True(outcome.Hit);
        Assert.Equal(AttackerAttackPower, outcome.DamageApplied);
    }

    [Fact]
    public void LevelAboveThreshold_ZeroPenalty_NoReduction()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(113), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), attackerAttackBudgetEnforced: false, attackerActionSkillNumber: 0,
            attackerActionSkillGradePoints: 0, attackerSymbolDamageDownPenalty: 0f);

        Assert.True(outcome.Hit);
        Assert.Equal(AttackerAttackPower, outcome.DamageApplied);
    }

    [Fact]
    public void DefaultParameter_OmittedEntirely_BehavesAsUnmalused_ExistingCallersUnaffected()
    {
        // Every existing positional call site (Zone.Combat.cs before this session, and any test predating this
        // change) omits the new trailing parameter entirely -- must default to "never malused".
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(200), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), false, 0, 0);

        Assert.True(outcome.Hit);
        Assert.Equal(AttackerAttackPower, outcome.DamageApplied);
    }
}

using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Skills;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Combat;

/// <summary>
///     PvP attack resolution (<c>mCase</c> 2, enemy-tribe) -- pure port of <c>AttackPlayer</c>
///     (S07_MyGame02.cpp:886-1416). Never mutates state itself; caller applies the outcome.
/// </summary>
/// <remarks>
///     Not implemented: duel (<c>mCase</c> 1), PvM/MvP/stun (handled elsewhere or unmodeled), Holy Shield, PvP kill
///     rewards -- a PvP kill here only applies HP death, no CP/XP/drop to the killer.
///     Also not implemented: the tribe "Formation Skill" x1.1 ATK/DEF modifier gated on
///     <c>mWorldInfo->mTribeMasterCallAbility[tribe]</c> (S07_MyGame02.cpp:1071-1079) -- that world-scope buff state isn't
///     wired up anywhere yet (see <see cref="Fenrir.Application.Game.Handlers.Tribes.TribeActionHandler" /> tSort 5, which
///     always aborts because its own gating flag is never set).
///     PRESERVED VERBATIM: after the min-5 floor and crit doubling, damage is divided by
///     <see cref="MinimumDamageAgainstAvatar" /> (5) -- verified at two call sites, absent from PvM. Makes PvP damage ~5x
///     lower than raw ATK-DEF suggests; do not "fix".
/// </remarks>
public static class CombatResolver
{
    public const float MaxAttackDistance = 185.0f;

    /// <summary>Also the PvP-only final divisor -- see class remarks.</summary>
    public const int MinimumDamageAgainstAvatar = 5;

    /// <summary>20 legacy ticks = 10s anti-chain-attack window after either side last took damage.</summary>
    public const int ProtectTickLegacyTicks = 20;

    /// <summary>Skill 78 is excluded from the crit roll; unexplained in the source.</summary>
    private const int SkillNumberExcludedFromCritical = 78;

    public static readonly TimeSpan ProtectDuration = SimulationClock.ToTimeSpan(ProtectTickLegacyTicks);

    public static AttackOutcome ResolveEnemyTribeAttack(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        AttackForProtocol request,
        TimeSpan zoneClock,
        SkillDefinition? attackSkill,
        IRandomSource rng)
    {
        if (attacker.CharacterId == defender.CharacterId)
            return AttackOutcome.Reject(AttackRejectReason.SameCharacter);
        if (attacker.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (defender.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        // Alliance is not modeled -- only the plain same-tribe guard is reproduced (strictly less restrictive).
        if (attacker.Tribe == defender.Tribe)
            return AttackOutcome.Reject(AttackRejectReason.SameOrAlliedTribe);
        if (attacker.ZoneEntryAtZoneClock is { } attackerZoneEntry &&
            zoneClock - attackerZoneEntry < ProtectDuration)
            return AttackOutcome.Reject(AttackRejectReason.AttackerProtected);
        if (defender.ZoneEntryAtZoneClock is { } defenderZoneEntry &&
            zoneClock - defenderZoneEntry < ProtectDuration)
            return AttackOutcome.Reject(AttackRejectReason.DefenderProtected);
        if (!CombatMath.IsInRange(attacker.PosX, attacker.PosY, attacker.PosZ, defender.PosX, defender.PosY,
                defender.PosZ, MaxAttackDistance))
            return AttackOutcome.Reject(AttackRejectReason.OutOfRange);

        var attackSuccess = attacker.Stats.AttackSuccess;
        if (attackSuccess < 1)
            return AttackOutcome.Reject(AttackRejectReason.AttackerHasNoAttackSuccess);

        // Spent the moment the attack is attempted, before the hit-chance roll, win or miss.
        var chargeConsumed = attacker.ChargeBuffPercent > 0;

        var attackBlock = defender.Stats.AttackBlock;
        if (attackBlock > 0)
        {
            var hitChance = CombatMath.ComputeHitChancePercent(attackSuccess, attackBlock);
            if (!CombatMath.RollHit(hitChance, rng))
                return AttackOutcome.Miss(chargeConsumed);
        }

        var isSkillAttack = request.AttackActionValue1 == 2;

        var attackPower = attacker.Stats.AttackPower;
        if (isSkillAttack && attackSkill != null)
        {
            var ratio = SkillCatalog.ReturnSkillValue(attackSkill, request.AttackActionValue3,
                SkillValueKind.AttackPowerRatio);
            if (ratio > 0f)
                attackPower = CombatMath.ApplySkillPowerRatio(attackPower, ratio);
        }

        var damage = attackPower - defender.Stats.DefensePower;
        if (damage < 1) damage = 1;

        if (chargeConsumed)
            damage = (int)(damage * (attacker.ChargeBuffPercent + 100) * 0.01f);

        damage = CombatMath.ApplyVariance(damage, rng);
        if (damage < MinimumDamageAgainstAvatar) damage = MinimumDamageAgainstAvatar;

        var critical = false;
        if (CanRollCritical(request, attackSkill))
        {
            var criticalChance = attacker.Stats.Critical - defender.Stats.CriticalDefence;
            if (criticalChance > 0 && CombatMath.RollCritical(criticalChance, rng))
            {
                damage *= 2;
                critical = true;
            }
        }

        damage /= MinimumDamageAgainstAvatar; // PvP-only division -- see class remarks

        var elementDamage = 0;
        if (attacker.Stats.ElementAttackPower > defender.Stats.ElementDefensePower)
            elementDamage = attacker.Stats.ElementAttackPower - defender.Stats.ElementDefensePower;
        damage += elementDamage;

        if (damage > defender.Life)
            damage = defender.Life;

        return new AttackOutcome(false, AttackRejectReason.None, true, critical, damage, elementDamage,
            chargeConsumed);
    }

    /// <summary>Melee always rolls; a skill attack only rolls when the skill isn't 78 and its AttackType is 2 or 5.</summary>
    private static bool CanRollCritical(AttackForProtocol request, SkillDefinition? attackSkill)
    {
        if (request.AttackActionValue1 == 1)
            return true;
        if (request.AttackActionValue1 != 2)
            return false;
        if (request.AttackActionValue2 == SkillNumberExcludedFromCritical)
            return false;

        return attackSkill is { Skill.AttackType: 2 or 5 };
    }
}

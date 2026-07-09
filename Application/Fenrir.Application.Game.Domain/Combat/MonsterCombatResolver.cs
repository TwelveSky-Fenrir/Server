using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     PvM/MvP attack resolution -- monster-entity twin of <see cref="CombatResolver" />'s PvP path
///     (ProcessAttack03/04).
/// </summary>
/// <remarks>
///     Verified divergence, not a bug: neither direction applies PvP's ÷5 division nor
///     <see cref="CombatResolver.MinimumDamageAgainstAvatar" />'s floor.
/// </remarks>
public static class MonsterCombatResolver
{
    /// <summary>
    ///     The sole LNW33-build owner-name-lock exemption (<c>Server/ts25zone/S07_MyGame02.cpp:1890</c>): this
    ///     one monster template skips the name-match requirement entirely, subject instead to
    ///     <see cref="OwnerNameLockExemptionCooldown" />.
    /// </summary>
    private const int OwnerNameLockExemptMonsterId = 9002;

    /// <summary>
    ///     <c>GetMinuteFromTick(1.0f)</c> (<c>Server/ts25zone/S07_MyGame02.cpp:1893</c>) -- the elapsed-time gate
    ///     for <see cref="OwnerNameLockExemptMonsterId" />'s owner-name-lock exemption, measured against
    ///     <see cref="World.Monsters.MonsterEntity.OwnerNameLockExemptionArmedAt" />.
    /// </summary>
    public static readonly TimeSpan OwnerNameLockExemptionCooldown = TimeSpan.FromMinutes(1);

    /// <param name="request">
    ///     <c>AttackActionValue1</c> (attack-mode selector, 1=melee/2=skill -- any other value is an
    ///     unconditional silent reject) and, only when it selects the skill branch,
    ///     <c>AttackActionValue2</c>/<c>AttackActionValue3</c> (skill number / combined skill-grade points),
    ///     consulted against <paramref name="attackerActionSkillNumber" />/
    ///     <paramref name="attackerActionSkillGradePoints" /> below (<c>S07_MyGame02.cpp:2171-2189</c>).
    /// </param>
    /// <param name="attackerAttackBudgetEnforced">
    ///     <see cref="World.PlayerRuntimeState.AttackBudgetEnforced" /> (legacy <c>mCheckMaxAttackPacketNum</c>)
    ///     -- gates the skill-number/skill-grade echo check below. Deliberately NOT
    ///     <see cref="World.PlayerRuntimeState.VerifyEchoedActionState" />: that is a separate, narrower toggle
    ///     that only gates <see cref="StunResolver" />/<see cref="UnstunResolver" />'s own echo re-check
    ///     (<c>S07_MyGame02.cpp:3593-3600</c>/<c>:3783-3798</c>); ProcessAttack03's echo check is gated by
    ///     <c>mCheckMaxAttackPacketNum</c> instead (confirmed directly at <c>:2177</c>), the same flag
    ///     <see cref="AttackPacketBudget" /> already reproduces for this same attacker earlier in the same
    ///     packet's resolution.
    /// </param>
    /// <param name="attackerActionSkillNumber"><see cref="World.PlayerRuntimeState.ActionSkillNumber" />.</param>
    /// <param name="attackerActionSkillGradePoints">
    ///     <see cref="World.PlayerRuntimeState.ActionSkillGradeNum1" /> +
    ///     <see cref="World.PlayerRuntimeState.ActionSkillGradeNum2" /> -- already the combined grade, not
    ///     summed here.
    /// </param>
    public static AttackOutcome ResolvePvmAttack(
        CombatantSnapshot attacker,
        MonsterEntity monster,
        AttackForProtocol request,
        TimeSpan zoneClock,
        IRandomSource rng,
        bool attackerAttackBudgetEnforced,
        int attackerActionSkillNumber,
        int attackerActionSkillGradePoints)
    {
        if (attacker.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (monster.Life < 1)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        // Only the attacker's protect-tick applies -- monsters have no protect-tick of their own.
        if (attacker.ZoneEntryAtZoneClock is { } attackerZoneEntry &&
            zoneClock - attackerZoneEntry < CombatResolver.ProtectDuration)
            return AttackOutcome.Reject(AttackRejectReason.AttackerProtected);

        // Owner-name lock (S07_MyGame02.cpp:1885-1896): runs before the distance check, matching legacy's own
        // gate order. Unreachable in production today -- no Fenrir spawn path sets MonsterEntity.OwnerName --
        // but kept wired so a future summon-monster mechanic gets correct enforcement for free instead of
        // silently under-enforcing ownership the moment it lands.
        if (!string.IsNullOrEmpty(monster.OwnerName) &&
            !string.Equals(monster.OwnerName, attacker.Name, StringComparison.Ordinal))
        {
            var exempt = monster.Template.MonsterId == OwnerNameLockExemptMonsterId &&
                         monster.OwnerNameLockExemptionArmedAt is { } armedAt &&
                         zoneClock - armedAt >= OwnerNameLockExemptionCooldown;
            if (!exempt)
                return AttackOutcome.Reject(AttackRejectReason.OwnerNameLocked);
        }

        if (!CombatMath.IsInRange(attacker.PosX, attacker.PosY, attacker.PosZ, monster.PosX, monster.PosY,
                monster.PosZ, CombatResolver.MaxAttackDistance))
            return AttackOutcome.Reject(AttackRejectReason.OutOfRange);

        // Attack-mode selector switch (S07_MyGame02.cpp:2171-2189): both recognized cases fall through to the
        // identical GetAttackSuccess() read immediately below (reproduced as the shared attackSuccess line
        // that follows this switch); only the skill case (2) carries the extra echo sub-check, and only while
        // the attacker's own rate-check flag is on.
        switch (request.AttackActionValue1)
        {
            case 1:
                break;
            case 2:
                if (attackerAttackBudgetEnforced &&
                    (request.AttackActionValue2 != attackerActionSkillNumber ||
                     request.AttackActionValue3 != attackerActionSkillGradePoints))
                    return AttackOutcome.Reject(AttackRejectReason.AntiCheatEchoMismatch);
                break;
            default:
                return AttackOutcome.Reject(AttackRejectReason.InvalidAttackModeSelector);
        }

        var attackSuccess = attacker.Stats.AttackSuccess;
        if (attackSuccess < 1)
            return AttackOutcome.Reject(AttackRejectReason.AttackerHasNoAttackSuccess);

        var chargeConsumed = attacker.ChargeBuffPercent > 0;

        var attackBlock = monster.Template.AttackBlock;
        if (attackBlock > 0)
        {
            var hitChance = CombatMath.ComputeHitChancePercent(attackSuccess, attackBlock);
            if (!CombatMath.RollHit(hitChance, rng))
                return AttackOutcome.Miss(chargeConsumed);
        }

        var damage = attacker.Stats.AttackPower - monster.Template.DefensePower;
        if (damage < 1) damage = 1;

        if (chargeConsumed)
            damage = (int)(damage * (attacker.ChargeBuffPercent + 100) * 0.01f);

        damage = CombatMath.ApplyVariance(damage, rng);
        if (damage < 1) damage = 1;

        var critical = false;
        if (CombatMath.RollCritical(attacker.Stats.Critical, rng)) // monsters have no CriticalDefence stat
        {
            damage *= 2;
            critical = true;
        }

        var elementDamage = 0;
        if (attacker.Stats.ElementAttackPower > monster.Template.ElementDefensePower)
            elementDamage = attacker.Stats.ElementAttackPower - monster.Template.ElementDefensePower;
        damage += elementDamage;

        // "View" damage (S07_MyGame02.cpp:2371) is captured BEFORE the life-cap clamp (:2372-2375); "real"
        // damage (:2376) is the clamped value -- same view-before-clamp / real-after-clamp split as PvP.
        var viewDamage = damage;
        if (damage > monster.Life)
            damage = monster.Life;

        return new AttackOutcome(false, AttackRejectReason.None, true, critical, damage, viewDamage, elementDamage,
            chargeConsumed);
    }

    /// <summary>
    ///     AI-initiated -- intended caller is <see cref="Monsters.MonsterAiSystem" />'s attack-windup state, not
    ///     <c>Zone.ApplyCombatCommand</c>.
    /// </summary>
    public static AttackOutcome ResolveMvpAttack(
        MonsterEntity monster,
        CombatantSnapshot defender,
        TimeSpan zoneClock,
        IRandomSource rng)
    {
        if (monster.Life < 1)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (defender.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        if (defender.ZoneEntryAtZoneClock is { } defenderZoneEntry &&
            zoneClock - defenderZoneEntry < CombatResolver.ProtectDuration)
            return AttackOutcome.Reject(AttackRejectReason.DefenderProtected);

        // MvP's range check is XZ-only, bounded by the monster's own radius -- not the shared 3D 185.0f check.
        var dx = monster.PosX - defender.PosX;
        var dz = monster.PosZ - defender.PosZ;
        var attackRadius = (float)monster.Template.RadiusInfo2;
        if (dx * dx + dz * dz > attackRadius * attackRadius)
            return AttackOutcome.Reject(AttackRejectReason.OutOfRange);

        var attackSuccess = monster.Template.AttackSuccess;
        if (attackSuccess < 1)
            return AttackOutcome.Reject(AttackRejectReason.AttackerHasNoAttackSuccess);

        var attackBlock = defender.Stats.AttackBlock;
        if (attackBlock > 0)
        {
            var hitChance = CombatMath.ComputeHitChancePercent(attackSuccess, attackBlock);
            if (!CombatMath.RollHit(hitChance, rng))
                return AttackOutcome.Miss();
        }

        var damage = monster.Template.AttackPower - defender.Stats.DefensePower;
        if (damage < 1) damage = 1;

        damage = CombatMath.ApplyVariance(damage, rng);
        if (damage < 1) damage = 1;

        // Flat 1% crit chance even when Critical does not exceed CriticalDefence (verified asymmetry vs PvP/PvM).
        var criticalChance = monster.Template.Critical - defender.Stats.CriticalDefence;
        var critical = criticalChance > 0
            ? CombatMath.RollCritical(criticalChance, rng)
            : CombatMath.RollCritical(1, rng);
        if (critical)
            damage *= 2;

        var elementDamage = 0;
        if (monster.Template.ElementAttackPower > defender.Stats.ElementDefensePower)
            elementDamage = monster.Template.ElementAttackPower - defender.Stats.ElementDefensePower;
        damage += elementDamage;

        // "View" damage (S07_MyGame02.cpp:3428) is captured BEFORE the life-cap clamp (:3429-3432); "real"
        // damage (:3433) is the clamped value -- same view-before-clamp / real-after-clamp split as PvP/PvM.
        var viewDamage = damage;
        if (damage > defender.Life)
            damage = defender.Life;

        return new AttackOutcome(false, AttackRejectReason.None, true, critical, damage, viewDamage, elementDamage,
            false);
    }
}

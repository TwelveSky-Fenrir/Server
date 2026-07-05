using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Network.Serialization.Packets.Shared;

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
    public static AttackOutcome ResolvePvmAttack(
        CombatantSnapshot attacker,
        MonsterEntity monster,
        AttackForProtocol request,
        TimeSpan zoneClock,
        IRandomSource rng)
    {
        _ = request;

        if (attacker.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (monster.Life < 1)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        // Only the attacker's protect-tick applies -- monsters have no protect-tick of their own.
        if (attacker.ZoneEntryAtZoneClock is { } attackerZoneEntry &&
            zoneClock - attackerZoneEntry < CombatResolver.ProtectDuration)
            return AttackOutcome.Reject(AttackRejectReason.AttackerProtected);
        if (!CombatMath.IsInRange(attacker.PosX, attacker.PosY, attacker.PosZ, monster.PosX, monster.PosY,
                monster.PosZ, CombatResolver.MaxAttackDistance))
            return AttackOutcome.Reject(AttackRejectReason.OutOfRange);

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

        if (damage > monster.Life)
            damage = monster.Life;

        return new AttackOutcome(false, AttackRejectReason.None, true, critical, damage, elementDamage, chargeConsumed);
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

        if (damage > defender.Life)
            damage = defender.Life;

        return new AttackOutcome(false, AttackRejectReason.None, true, critical, damage, elementDamage, false);
    }
}

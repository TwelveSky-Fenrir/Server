using Fenrir.Application.Game.World.Monsters;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Combat;

/// <summary>
///     PvM (<c>mCase</c> 3, "Avatar -&gt; Monster") and MvP (<c>mCase</c> 4, "Monster -&gt; Avatar") attack
///     resolution -- the monster-entity twin of <see cref="CombatResolver" />'s own PvP path, verified
///     directly against <c>Server/ts25zone/S07_MyGame02.cpp:1825-2310</c> (<c>ProcessAttack03</c>) and
///     <c>:3179-3437</c> (<c>ProcessAttack04</c>). Reuses <see cref="CombatMath" />'s primitives (already
///     general enough for this, per <see cref="CombatResolver" />'s own remarks) for the hit-chance/variance
///     math verified IDENTICAL across all three attack directions.
/// </summary>
/// <remarks>
///     VERIFIED DIVERGENCE FROM PvP (D8 iso-behavior, not a bug to "fix"): NEITHER direction here applies the
///     PvP-only ÷5 damage division nor <see cref="CombatResolver.MinimumDamageAgainstAvatar" />'s floor --
///     <see cref="CombatResolver" />'s own remarks already flagged <c>ProcessAttack03</c> as lacking it; this
///     type additionally verifies <c>ProcessAttack04</c>'s body the same way (read in full for this port --
///     neither the division nor the floor appears anywhere in it). MvP's critical roll is ALSO asymmetric vs
///     PvP/PvM: when the monster's <c>Critical</c> stat does not exceed the defender's <c>CriticalDefence</c>,
///     the legacy still rolls a FLAT 1% critical chance (<c>ProcessAttack04</c> l.3315-3321) instead of simply
///     skipping the roll the way PvP/PvM do -- preserved exactly in <see cref="ResolveMvpAttack" />.
///     <para>
///     NOT ported (explicit scope cut, same posture as <see cref="CombatResolver" />): tribe-symbol damage
///     up/down, the server-wide PvM ratio (<c>mServerPVM</c>), zone-specific special-type bonuses (catapult
///     +15000 attack power, Yanggok/Zone175/Zone195 hooks), Holy Shield absorption/removal, and the ~12
///     magic-monster-ID special cases report 05 §4/§5 themselves document as mostly dead/disabled in this
///     build. Skill-based attacks (<c>AttackActionValue1 == 2</c>) against/from a monster are simplified to
///     PLAIN melee power (no skill attack-power-ratio bonus) and an UNCONDITIONAL critical-roll eligibility
///     (the legacy's skill-78-exclusion/attack-type-2-or-5 gate is not reproduced for this direction) --
///     documented open issue, not silently dropped: melee (<c>ActionActionValue1 == 1</c>) is the common case
///     and is fully faithful.
///     </para>
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
        // Verified: ProcessAttack03 only checks the ATTACKER's own protect-tick window -- monsters have no
        // "protect tick" concept of their own in this path. Null (never entered a zone yet) never rejects --
        // see CombatantSnapshot.ZoneEntryAtZoneClock's own remarks on why that is NOT the same as TimeSpan.Zero.
        if (attacker.ZoneEntryAtZoneClock is { } attackerZoneEntry &&
            zoneClock - attackerZoneEntry < CombatResolver.ProtectDuration)
            return AttackOutcome.Reject(AttackRejectReason.AttackerProtected);
        if (!CombatMath.IsInRange(attacker.PosX, attacker.PosY, attacker.PosZ, monster.PosX, monster.PosY,
                monster.PosZ, CombatResolver.MaxAttackDistance))
            return AttackOutcome.Reject(AttackRejectReason.OutOfRange);

        var attackSuccess = attacker.Stats.AttackSuccess;
        if (attackSuccess < 1)
            return AttackOutcome.Reject(AttackRejectReason.AttackerHasNoAttackSuccess);

        // Same "spent on attempt, not on hit" fix as CombatResolver.ResolveEnemyTribeAttack -- see its remarks.
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
        if (CombatMath.RollCritical(attacker.Stats.Critical, rng)) // no monster CriticalDefence stat -- see class remarks
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
    ///     AI-initiated (report 05 §3/§4: the monster's OWN <c>Update</c> loop calls <c>ProcessAttack04</c>
    ///     directly, <c>S07_MyGame05.cpp:3961</c> -- never dispatched from a client packet in practice). The
    ///     intended caller is <see cref="Monsters.MonsterAiSystem" />'s attack-windup state, NOT
    ///     <c>Zone.ApplyCombatCommand</c> -- see that system's own remarks.
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

        // Verified: ProcessAttack04's OWN range check is XZ-only (GetDoubleX_Z), bounded by the monster's own
        // mRadiusInfo[1]^2 -- NOT the shared 3D CheckInRange/185.0f constant PvP/PvM both use.
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

        // Verified asymmetric fallback (class remarks): still a flat 1% crit chance even when the monster's
        // Critical stat does not exceed the defender's CriticalDefence.
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

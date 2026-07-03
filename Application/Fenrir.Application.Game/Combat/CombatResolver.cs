using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Skills;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Combat;

/// <summary>
///     Player-vs-player attack resolution (<c>mCase</c> 2, "Avatar -&gt; Avatar (difference clan)") -- a pure
///     C# port of <c>AttackPlayer</c> (verified in full, <c>Server/ts25zone/S07_MyGame02.cpp:886-1416</c>) for
///     the ENEMY-tribe path. <c>Zone.ApplyCombatCommand</c> is the only caller: it snapshots both
///     <c>PlayerRuntimeState</c>s into <see cref="CombatantSnapshot" />, calls this, then applies the
///     <see cref="AttackOutcome" /> back onto the live state (HP, broadcast, <c>ApplyDeath</c>) -- this type
///     itself never mutates anything (single-writer invariant, architecture reference §10.1).
/// </summary>
/// <remarks>
///     SCOPE for this pass (report 05 §4's other guards/mechanics, deliberately NOT reproduced -- see this
///     task's StructuredOutput for the full list):
///     <list type="bullet">
///         <item><c>mCase</c> 1 (DUEL): the legacy gates it on a 3-way duel-state match
///             (<c>aDuelState[0..2]</c>) Fenrir has no subsystem for yet -- NOT implemented; a client sending
///             Case=1 is silently dropped by <c>Zone.ApplyCombatCommand</c> (no fabricated duel state to
///             validate against).</item>
///         <item><c>mCase</c> 3/4 (PvM/MvP), 5/6 (stun/unstun): monsters and the stun subsystem do not exist
///             yet (V4's job) -- <c>Zone.ApplyCombatCommand</c> drops these too. <see cref="CombatMath" />'s
///             primitives are already general enough for V4 to reuse once monster entities exist.</item>
///         <item>Holy Shield absorption / Return Damage / shield removal (buff slots 9/12/14), tribe-master-call
///             formation bonuses, zone-124's &lt;10s triple-crit, PvP kill rewards (<c>ProcessForKillOtherTribe</c>,
///             report 05 §6) -- all need zone/tribe/alliance state this pass does not model. A PvP kill here
///             only calls <c>Zone.ApplyDeath</c> (HP mechanism); it grants no CP/XP/drop to the killer.</item>
///     </list>
///     PRESERVED VERBATIM despite looking like a bug (D8 iso-behavior policy): after the min-5 floor and the
///     possible critical doubling, avatar-vs-avatar damage is divided by <see cref="MinimumDamageAgainstAvatar" />
///     (=5) -- verified at TWO independent call sites (<c>AttackPlayer</c> l.1157 DUEL branch, l.1256 ENEMY
///     branch), NOT present in <c>ProcessAttack03</c> (PvM, verified separately, no such division). Net effect:
///     a non-critical PvP hit's damage floor becomes 1 (5/5) and a critical's becomes 2 (10/5) -- PvP damage
///     output is effectively 5x lower than the raw ATK-DEF number would suggest. Do not "fix" this.
/// </remarks>
public static class CombatResolver
{
    /// <summary><c>tMaxAttackDistance</c> (S07_MyGame02.cpp:512).</summary>
    public const float MaxAttackDistance = 185.0f;

    /// <summary><c>tMinDamageValueWithAvatar</c> (S07_MyGame02.cpp:511) -- both a damage FLOOR and the PvP-only final divisor (class remarks).</summary>
    public const int MinimumDamageAgainstAvatar = 5;

    /// <summary><c>PROTECT_TICK</c> (S07_MyGame02.cpp:9) -- 20 legacy ticks = 10s anti-chain-attack window after either side last took damage.</summary>
    public const int ProtectTickLegacyTicks = 20;

    public static readonly TimeSpan ProtectDuration = LegacyTime.ToTimeSpan(ProtectTickLegacyTicks);

    /// <summary>
    ///     Skill number 78 is explicitly excluded from the critical roll at its OWN call site (l.1123, l.1205) --
    ///     preserved verbatim, not otherwise explained in the source.
    /// </summary>
    private const int SkillNumberExcludedFromCritical = 78;

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
        // Alliance is not modeled (report §4 "défenser->aTribe == ReturnAllianceTribe(attacker)") -- only the
        // plain same-tribe guard is reproduced; a real alliance would be strictly MORE restrictive than this.
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

        // Spent the moment the attack is ATTEMPTED (AttackOutcome.Miss's own remarks) -- BEFORE the hit-chance
        // roll, win or miss, not only folded into the post-hit damage math like a prior pass here had it.
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
        if (isSkillAttack && attackSkill is { } skill)
        {
            var ratio = SkillCatalog.ReturnSkillValue(skill, request.AttackActionValue3,
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

        // Verified PvP-only quirk -- see class remarks. Integer division, matching the C++'s own `int /= int`.
        damage /= MinimumDamageAgainstAvatar;

        var elementDamage = 0;
        if (attacker.Stats.ElementAttackPower > defender.Stats.ElementDefensePower)
            elementDamage = attacker.Stats.ElementAttackPower - defender.Stats.ElementDefensePower;
        damage += elementDamage;

        if (damage > defender.Life)
            damage = defender.Life;

        return new AttackOutcome(false, AttackRejectReason.None, true, critical, damage, elementDamage,
            chargeConsumed);
    }

    /// <summary>
    ///     Melee (ActionValue1==1) can always roll; a skill attack (==2) only rolls when the skill isn't 78 and
    ///     its <c>AttackType</c> (<c>ReturnAttackType</c>) is 2 or 5 (l.1117-1134/1203-1252, identical gate
    ///     repeated for both critical branches).
    /// </summary>
    private static bool CanRollCritical(AttackForProtocol request, SkillDefinition? attackSkill)
    {
        if (request.AttackActionValue1 == 1)
            return true;
        if (request.AttackActionValue1 != 2)
            return false;
        if (request.AttackActionValue2 == SkillNumberExcludedFromCritical)
            return false;

        return attackSkill is { } skill && skill.Skill.AttackType is 2 or 5;
    }
}

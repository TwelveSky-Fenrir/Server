using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Combat;

public static class CombatResolver
{
    public const float MaxAttackDistance = 185.0f;

    public const int MinimumDamageAgainstAvatar = 5;

    public const int ProtectTickLegacyTicks = 20;

    private const int SkillNumberExcludedFromCritical = 78;

    private const int NoActionYetSort = 0;

    private const int DeathPoseSort = 12;

    public static readonly TimeSpan ProtectDuration = SimulationClock.ToTimeSpan(ProtectTickLegacyTicks);

    public static AttackOutcome ResolveEnemyTribeAttack(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        AttackForProtocol request,
        TimeSpan zoneClock,
        SkillDefinition? attackSkill,
        IRandomSource rng,
        bool zoneAllowsEnemyTribeAttack = true,
        bool sameTribeAttackExempt = false,
        bool newbieProtectionZone = false,
        bool defenderPshopOpen = false,
        int defenderActionSort = 1,
        byte? allyOfAttackerTribe = null,
        byte attackerFormationCode = FormationCombatResolver.NoFormation,
        byte defenderFormationCode = FormationCombatResolver.NoFormation)
    {
        if (attacker.CharacterId == defender.CharacterId)
            return AttackOutcome.Reject(AttackRejectReason.SameCharacter);
        if (attacker.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (defender.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        if (defenderPshopOpen)
            return AttackOutcome.Reject(AttackRejectReason.DefenderShopOpen);
        if (defenderActionSort is NoActionYetSort or DeathPoseSort)
            return AttackOutcome.Reject(AttackRejectReason.DefenderActionStateBlocksTargeting);
        if (!zoneAllowsEnemyTribeAttack)
            return AttackOutcome.Reject(AttackRejectReason.ZonePvpDisabled);
        if (!sameTribeAttackExempt && (attacker.Tribe == defender.Tribe || defender.Tribe == allyOfAttackerTribe))
            return AttackOutcome.Reject(AttackRejectReason.SameOrAlliedTribe);
        if (newbieProtectionZone && attacker.Level >= 90 && defender.Level < 90)
            return AttackOutcome.Reject(AttackRejectReason.NewbieProtectionLevelGap);

        return ResolveDamage(attacker, defender, request, zoneClock, attackSkill, rng, attackerFormationCode,
            defenderFormationCode);
    }

    public static AttackOutcome ResolveDuelAttack(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        AttackForProtocol request,
        TimeSpan zoneClock,
        SkillDefinition? attackSkill,
        IRandomSource rng,
        bool attackerAndDefenderShareActiveDuel,
        bool defenderPshopOpen,
        int defenderActionSort,
        bool zone124OverrideActive = false)
    {
        if (attacker.CharacterId == defender.CharacterId)
            return AttackOutcome.Reject(AttackRejectReason.SameCharacter);
        if (attacker.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (defender.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        if (defenderPshopOpen)
            return AttackOutcome.Reject(AttackRejectReason.DefenderShopOpen);
        if (defenderActionSort is NoActionYetSort or DeathPoseSort)
            return AttackOutcome.Reject(AttackRejectReason.DefenderActionStateBlocksTargeting);
        if (!attackerAndDefenderShareActiveDuel)
            return AttackOutcome.Reject(AttackRejectReason.DuelNotAuthorized);

        return ResolveDamage(attacker, defender, request, zoneClock, attackSkill, rng,
            zone124OverrideActive: zone124OverrideActive);
    }

    private static AttackOutcome ResolveDamage(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        AttackForProtocol request,
        TimeSpan zoneClock,
        SkillDefinition? attackSkill,
        IRandomSource rng,
        byte attackerFormationCode = FormationCombatResolver.NoFormation,
        byte defenderFormationCode = FormationCombatResolver.NoFormation,
        bool zone124OverrideActive = false)
    {
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

        attackPower = FormationCombatResolver.ScaleAttackPower(attackPower, attackerFormationCode);
        var defensePower =
            FormationCombatResolver.ScaleDefensePower(defender.Stats.DefensePower, defenderFormationCode);
        var damage = attackPower - defensePower;
        if (damage < 1) damage = 1;

        if (chargeConsumed)
            damage = (int)(damage * (attacker.ChargeBuffPercent + 100) * 0.01f);

        damage = CombatMath.ApplyVariance(damage, rng);
        if (damage < MinimumDamageAgainstAvatar) damage = MinimumDamageAgainstAvatar;

        var critical = false;
        if (CanRollCritical(request, attackSkill))
        {
            var criticalChance = FormationCombatResolver.AdjustCriticalChance(
                attacker.Stats.Critical, defender.Stats.CriticalDefence, attackerFormationCode, defenderFormationCode);
            if (criticalChance > 0 && CombatMath.RollCritical(criticalChance, rng))
            {
                damage *= 2;
                critical = true;
            }
        }

        if (zone124OverrideActive)
        {
            damage *= Zone124DuelOverrideResolver.DamageMultiplier;
            critical = true;
        }

        damage /= MinimumDamageAgainstAvatar;

        var elementDamage = 0;
        if (attacker.Stats.ElementAttackPower > defender.Stats.ElementDefensePower)
            elementDamage = attacker.Stats.ElementAttackPower - defender.Stats.ElementDefensePower;
        damage += elementDamage;

        var viewDamage = damage;
        if (damage > defender.Life)
            damage = defender.Life;

        return new AttackOutcome(false, AttackRejectReason.None, true, critical, damage, viewDamage, elementDamage,
            chargeConsumed);
    }

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

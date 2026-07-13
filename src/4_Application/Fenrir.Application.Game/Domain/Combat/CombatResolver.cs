using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Combat;

public static class CombatResolver
{
    public const float MaxAttackDistance = 185.0f;

    public const int MinimumDamageAgainstAvatar = 5;

    public const int ProtectTickLegacyTicks = 20;

    private const int NoActionYetSort = 0;

    private const int DeathPoseSort = 12;

    public static readonly TimeSpan ProtectDuration = SimulationClock.ToTimeSpan(ProtectTickLegacyTicks);

    public static bool IsTargetableActionState(int actionSort)
    {
        return actionSort is not (NoActionYetSort or DeathPoseSort);
    }

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
        byte defenderFormationCode = FormationCombatResolver.NoFormation,
        bool attackerAttackBudgetEnforced = false,
        int attackerActionSkillNumber = 0,
        int attackerActionSkillGradePoints = 0)
    {
        if (attacker.CharacterId == defender.CharacterId)
            return AttackOutcome.Reject(AttackRejectReason.SameCharacter);
        if (attacker.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (defender.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        if (defenderPshopOpen)
            return AttackOutcome.Reject(AttackRejectReason.DefenderShopOpen);
        if (!IsTargetableActionState(defenderActionSort))
            return AttackOutcome.Reject(AttackRejectReason.DefenderActionStateBlocksTargeting);
        if (!zoneAllowsEnemyTribeAttack)
            return AttackOutcome.Reject(AttackRejectReason.ZonePvpDisabled);
        if (!sameTribeAttackExempt && (attacker.Tribe == defender.Tribe || defender.Tribe == allyOfAttackerTribe))
            return AttackOutcome.Reject(AttackRejectReason.SameOrAlliedTribe);
        if (newbieProtectionZone && attacker.Level >= 90 && defender.Level < 90)
            return AttackOutcome.Reject(AttackRejectReason.NewbieProtectionLevelGap);

        return ResolveDamage(attacker, defender, request, zoneClock, attackSkill, rng, attackerFormationCode,
            defenderFormationCode, attackerAttackBudgetEnforced: attackerAttackBudgetEnforced,
            attackerActionSkillNumber: attackerActionSkillNumber,
            attackerActionSkillGradePoints: attackerActionSkillGradePoints);
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
        bool zone124OverrideActive = false,
        bool attackerAttackBudgetEnforced = false,
        int attackerActionSkillNumber = 0,
        int attackerActionSkillGradePoints = 0)
    {
        if (attacker.CharacterId == defender.CharacterId)
            return AttackOutcome.Reject(AttackRejectReason.SameCharacter);
        if (attacker.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (defender.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        if (defenderPshopOpen)
            return AttackOutcome.Reject(AttackRejectReason.DefenderShopOpen);
        if (!IsTargetableActionState(defenderActionSort))
            return AttackOutcome.Reject(AttackRejectReason.DefenderActionStateBlocksTargeting);
        if (!attackerAndDefenderShareActiveDuel)
            return AttackOutcome.Reject(AttackRejectReason.DuelNotAuthorized);

        return ResolveDamage(attacker, defender, request, zoneClock, attackSkill, rng,
            zone124OverrideActive: zone124OverrideActive,
            attackerAttackBudgetEnforced: attackerAttackBudgetEnforced,
            attackerActionSkillNumber: attackerActionSkillNumber,
            attackerActionSkillGradePoints: attackerActionSkillGradePoints);
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
        bool zone124OverrideActive = false,
        bool attackerAttackBudgetEnforced = false,
        int attackerActionSkillNumber = 0,
        int attackerActionSkillGradePoints = 0)
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

        var isSkillAttack = request.AttackActionValue1 == 2;
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

        var attackBlock = defender.Stats.AttackBlock;
        if (attackBlock > 0)
        {
            var hitChance = CombatMath.ComputeHitChancePercent(attackSuccess, attackBlock);
            if (!CombatMath.RollHit(hitChance, rng))
                return AttackOutcome.Miss(chargeConsumed);
        }

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

        // Elemental contribution (legacy S07_MyGame02.cpp:1339-1359, AttackPlayer PvP path).
        // Uses the attacker's RAW ElementAttackPower on purpose: the per-skill kind-8 elemental
        // ratio (SkillValueKind.ElementAttackPowerRatio, ReturnSkillValue factor 8) is applied only
        // under legacy case 3 (mAttackActionValue1 == 3), and the upstream mode switch above rejects
        // every value other than 1 and 2 via its default arm (legacy default: return,
        // S07_MyGame02.cpp:1001-1017) -- so case 3 is unreachable vestigial code in the shipped
        // ReleaseEU33 build and NEVER majorates elemental power for any player attack, including a
        // kind-7 skill attack (value1 == 2). Do NOT wire ElementAttackPowerRatio onto value1 == 2:
        // that would DIVERGE from legacy, not match it. The genuine parity requirement here is that
        // this raw (attacker - defender) elemental difference is added AFTER the /5 PvP reduction and
        // is itself NOT divided by 5 (the add sits outside the mode switch, so it also runs for a
        // normal value1 == 1 attack).
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
        return request.AttackActionValue1 switch
        {
            1 => true,
            2 => SkillCriticalEligibility.IsEligibleForSkillHit(request.AttackActionValue2, attackSkill),
            _ => false
        };
    }
}

using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Combat;

public static class MonsterCombatResolver
{
    private const int OwnerNameLockExemptMonsterId = 9002;

    public const int CatapultAttackPowerBonus = 15000;

    public const short MalusMinimumAttackerLevel = 112;

    public const int DamageUpBonusFlatPerIncrement = 500;

    public static readonly TimeSpan OwnerNameLockExemptionCooldown = TimeSpan.FromMinutes(1);

    public static AttackOutcome ResolvePvmAttack(
        CombatantSnapshot attacker,
        MonsterEntity monster,
        AttackForProtocol request,
        TimeSpan zoneClock,
        SkillDefinition? attackSkill,
        IRandomSource rng,
        bool attackerAttackBudgetEnforced,
        int attackerActionSkillNumber,
        int attackerActionSkillGradePoints,
        float attackerSymbolDamageDownPenalty = 0f,
        int attackerSymbolDamageUpBonusIncrementCount = 0,
        bool targetCategoryEligible = true,
        int attackerNokSanStoneDamageBonus = 0)
    {
        if (attacker.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (monster.Life < 1)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        if (attacker.ZoneEntryAtZoneClock is { } attackerZoneEntry &&
            zoneClock - attackerZoneEntry < CombatResolver.ProtectDuration)
            return AttackOutcome.Reject(AttackRejectReason.AttackerProtected);

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

        var chargeConsumed = attacker.ChargeBuffPercent > 0;

        if (!targetCategoryEligible)
            return AttackOutcome.Reject(AttackRejectReason.TargetCategoryIneligible, chargeConsumed);

        switch (request.AttackActionValue1)
        {
            case 1:
                break;
            case 2:
                if (attackerAttackBudgetEnforced &&
                    (request.AttackActionValue2 != attackerActionSkillNumber ||
                     request.AttackActionValue3 != attackerActionSkillGradePoints))
                    return AttackOutcome.Reject(AttackRejectReason.AntiCheatEchoMismatch, chargeConsumed);
                break;
            default:
                return AttackOutcome.Reject(AttackRejectReason.InvalidAttackModeSelector, chargeConsumed);
        }

        var attackSuccess = attacker.Stats.AttackSuccess;
        if (attackSuccess < 1)
            return AttackOutcome.Reject(AttackRejectReason.AttackerHasNoAttackSuccess, chargeConsumed);

        var attackBlock = monster.Template.AttackBlock;
        if (attackBlock > 0)
        {
            var hitChance = CombatMath.ComputeHitChancePercent(attackSuccess, attackBlock);
            if (!CombatMath.RollHit(hitChance, rng))
                return AttackOutcome.Miss(chargeConsumed);
        }

        var isSkillHit = request.AttackActionValue1 == 2;

        var attackPower = attacker.Stats.AttackPower;
        if (isSkillHit && attackSkill is not null)
        {
            var powerUpRatio = SkillCatalog.ReturnSkillValue(attackSkill, request.AttackActionValue3,
                SkillValueKind.AttackPowerRatio);
            if (powerUpRatio > 0f)
                attackPower = CombatMath.ApplySkillPowerRatio(attackPower, powerUpRatio);
        }

        var damage = attackPower - monster.Template.DefensePower;
        if (damage < 1) damage = 1;

        if (chargeConsumed)
            damage = (int)(damage * (attacker.ChargeBuffPercent + 100) * 0.01f);

        damage = CombatMath.ApplyVariance(damage, rng);
        if (damage < 1) damage = 1;

        var critical = false;
        var criticalEligible = !isSkillHit ||
                               SkillCriticalEligibility.IsEligibleForSkillHit(request.AttackActionValue2,
                                   attackSkill);
        if (criticalEligible && CombatMath.RollCritical(attacker.Stats.Critical, rng))
        {
            damage *= 2;
            critical = true;
        }

        var elementDamage = 0;
        if (attacker.Stats.ElementAttackPower > monster.Template.ElementDefensePower)
            elementDamage = attacker.Stats.ElementAttackPower - monster.Template.ElementDefensePower;
        damage += elementDamage;

        if (attackerSymbolDamageUpBonusIncrementCount > 0)
            damage += DamageUpBonusFlatPerIncrement * attackerSymbolDamageUpBonusIncrementCount;

        if (attackerSymbolDamageDownPenalty > 0f && attacker.Level > MalusMinimumAttackerLevel)
            damage -= (int)(damage * attackerSymbolDamageDownPenalty);

        damage += attackerNokSanStoneDamageBonus;


        var viewDamage = damage;
        if (damage > monster.Life)
            damage = monster.Life;

        return new AttackOutcome(false, AttackRejectReason.None, true, critical, damage, viewDamage, elementDamage,
            chargeConsumed);
    }

    public static AttackOutcome ResolveMvpAttack(
        MonsterEntity monster,
        CombatantSnapshot defender,
        TimeSpan zoneClock,
        IRandomSource rng,
        bool defenderHidden = false,
        bool defenderPshopOpen = false,
        bool targetCategoryEligible = true)
    {
        if (monster.Life < 1)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (defender.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        if (defender.IsMovingZone)
            return AttackOutcome.Reject(AttackRejectReason.DefenderChangingZone);
        if (defenderHidden)
            return AttackOutcome.Reject(AttackRejectReason.DefenderHidden);
        if (defenderPshopOpen)
            return AttackOutcome.Reject(AttackRejectReason.DefenderShopOpen);
        if (!targetCategoryEligible)
            return AttackOutcome.Reject(AttackRejectReason.TargetCategoryIneligible);
        if (defender.ZoneEntryAtZoneClock is { } defenderZoneEntry &&
            zoneClock - defenderZoneEntry < CombatResolver.ProtectDuration)
            return AttackOutcome.Reject(AttackRejectReason.DefenderProtected);

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

        var monsterAttackPower = monster.Template.AttackPower;
        if (monster.SpecialSort == MonsterSpecialSort.CarThrower)
            monsterAttackPower += CatapultAttackPowerBonus;

        var damage = monsterAttackPower - defender.Stats.DefensePower;
        if (damage < 1) damage = 1;

        damage = CombatMath.ApplyVariance(damage, rng);
        if (damage < 1) damage = 1;

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

        var viewDamage = damage;
        if (damage > defender.Life)
            damage = defender.Life;

        return new AttackOutcome(false, AttackRejectReason.None, true, critical, damage, viewDamage, elementDamage,
            false);
    }

    public static bool RollHolyShieldRemoval(int monsterSpecialType, int attackSubMode, IRandomSource rng)
    {
        var threshold = HolyShieldRemovalThreshold(monsterSpecialType, attackSubMode);
        return threshold > 0 && rng.NextInt32(100) < threshold;
    }

    private static int HolyShieldRemovalThreshold(int monsterSpecialType, int attackSubMode)
    {
        return attackSubMode switch
        {
            0 => monsterSpecialType switch
            {
                35 or 36 or 37 or 38 or 40 => 10,
                41 => 15,
                42 => 20,
                43 => 25,
                44 => 30,
                _ => 0
            },
            1 => monsterSpecialType switch
            {
                35 or 36 or 37 or 38 or 40 => 50,
                41 => 60,
                42 => 70,
                43 => 80,
                44 => 90,
                _ => 0
            },
            _ => 0
        };
    }
}

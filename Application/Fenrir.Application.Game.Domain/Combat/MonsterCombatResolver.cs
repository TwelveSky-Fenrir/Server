using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Combat;

public static class MonsterCombatResolver
{

        private const int OwnerNameLockExemptMonsterId = 9002;

        public const int CatapultAttackPowerBonus = 15000;

        public static readonly TimeSpan OwnerNameLockExemptionCooldown = TimeSpan.FromMinutes(1);

        public const short MalusMinimumAttackerLevel = 112;

        public const int DamageUpBonusFlatPerIncrement = 500;

        public static AttackOutcome ResolvePvmAttack(
        CombatantSnapshot attacker,
        MonsterEntity monster,
        AttackForProtocol request,
        TimeSpan zoneClock,
        IRandomSource rng,
        bool attackerAttackBudgetEnforced,
        int attackerActionSkillNumber,
        int attackerActionSkillGradePoints,
        float attackerSymbolDamageDownPenalty = 0f,
        int attackerSymbolDamageUpBonusIncrementCount = 0)
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
        if (CombatMath.RollCritical(attacker.Stats.Critical, rng))
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
        IRandomSource rng)
    {
        if (monster.Life < 1)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (defender.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
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
}

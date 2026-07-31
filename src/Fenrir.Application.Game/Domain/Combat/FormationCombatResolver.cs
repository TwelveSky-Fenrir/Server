namespace Fenrir.Application.Game.Domain.Combat;

public static class FormationCombatResolver
{
    public const byte NoFormation = 0;

    public const byte AttackerPowerBoostCode = 1;

    public const byte DefenderDefenseBoostCode = 2;

    public const byte AttackerCriticalBoostCode = 3;

    public const byte DefenderCriticalReductionCode = 4;

    public const int CriticalThresholdShift = 5;

    public static int ScaleAttackPower(int attackPower, byte attackerFormationCode)
    {
        return attackerFormationCode == AttackerPowerBoostCode ? ScaleUpByTenth(attackPower) : attackPower;
    }

    public static int ScaleDefensePower(int defensePower, byte defenderFormationCode)
    {
        return defenderFormationCode == DefenderDefenseBoostCode ? ScaleUpByTenth(defensePower) : defensePower;
    }

    public static int AdjustCriticalChance(int attackerCritical, int defenderCriticalDefence,
        byte attackerFormationCode, byte defenderFormationCode)
    {
        var baseChance = attackerCritical - defenderCriticalDefence;
        if (baseChance < 0)
            baseChance = 0;
        return baseChance + CriticalThresholdDelta(attackerFormationCode, defenderFormationCode);
    }

    public static int CriticalThresholdDelta(byte attackerFormationCode, byte defenderFormationCode)
    {
        var attackerBoosts = attackerFormationCode == AttackerCriticalBoostCode;
        var defenderReduces = defenderFormationCode == DefenderCriticalReductionCode;
        if (attackerBoosts && !defenderReduces)
            return CriticalThresholdShift;
        if (defenderReduces && !attackerBoosts)
            return -CriticalThresholdShift;
        return 0;
    }

    private static int ScaleUpByTenth(int value)
    {
        return value + value / 10;
    }
}

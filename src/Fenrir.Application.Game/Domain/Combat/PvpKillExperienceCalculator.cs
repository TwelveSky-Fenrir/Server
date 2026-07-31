namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillExperienceCalculator
{
    public const int UnfavorableLevelGapZeroThreshold = 9;

    public const int WarriorScrollMultiplier = 2;

    public const int DoubleExpChargeMultiplier = 8;

    public const float DefaultZoneMultiplier = 1.0f;

    // Pas de base scalaire dans le legacy: baseAmount vient de la table par niveau de victime
    // puis du scaling d'ecart. Server/ts25zone/S07_MyGame03.cpp:2908
    public static int ComputeGain(
        int baseAmount,
        int attackerCombinedLevel,
        int defenderCombinedLevel,
        bool hasWarriorScrollBuff,
        bool hasDoubleExpCharge,
        float zoneMultiplier = DefaultZoneMultiplier)
    {
        if (baseAmount <= 0)
            return 0;

        if (attackerCombinedLevel - defenderCombinedLevel > UnfavorableLevelGapZeroThreshold)
            return 0;

        var amount = (int)(baseAmount * zoneMultiplier);

        if (hasWarriorScrollBuff)
            amount *= WarriorScrollMultiplier;
        if (hasDoubleExpCharge)
            amount *= DoubleExpChargeMultiplier;

        return amount;
    }
}

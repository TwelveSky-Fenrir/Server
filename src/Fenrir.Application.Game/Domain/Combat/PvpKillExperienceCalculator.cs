namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillExperienceCalculator
{
    // Server/ts25zone/GameSystem/GameSystem_01_Level.cpp:723 annule l'EXP seule; la porte a 13 de
    // Server/ts25zone/S07_MyGame03.cpp:2547 annule tout le kill. Ne jamais fusionner les deux seuils.
    public const int UnfavorableLevelGapZeroThreshold = 9;

    // Server/ts25zone/S07_MyGame03.cpp:2958-2961
    public const int WarriorScrollMultiplier = 2;

    // Server/ts25zone/S07_MyGame03.cpp:2963-2968
    public const int DoubleExpChargeMultiplier = 8;

    public static int ComputeGain(
        int baseAmount,
        int attackerCombinedLevel,
        int defenderCombinedLevel,
        bool hasWarriorScrollBuff,
        bool hasDoubleExpCharge,
        int zoneMultiplier)
    {
        if (baseAmount <= 0)
            return 0;

        if (attackerCombinedLevel - defenderCombinedLevel > UnfavorableLevelGapZeroThreshold)
            return 0;

        // Server/ts25zone/S07_MyGame03.cpp:2942-2951 multiplie en int: 150 sur les cartes ZONE049,
        // sinon KillOtherTribeExpRatio brut. Aucun arrondi flottant ne doit s'inserer ici.
        var amount = baseAmount * zoneMultiplier;

        if (hasWarriorScrollBuff)
            amount *= WarriorScrollMultiplier;
        if (hasDoubleExpCharge)
            amount *= DoubleExpChargeMultiplier;

        return amount;
    }
}

namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillPetExperienceCalculator
{
    // Server/BuildEU33/ServerInfo.ini:176 KillOtherTribePetExpRatio, lu brut (S07_MyGame01.cpp:449).
    // Forfait par kill, jamais un facteur: aucun multiplicateur PvP ne s'y applique.
    public const int DefaultPetExpRatio = 200;

    // Server/ts25zone/S07_MyGame03.cpp:2974 coupe sur aLevel1 SEUL contre LV_M1 (DEFINE.h:449). Le seul
    // ecrivain vivant de aLevel2 (S04_MyWork04.cpp:1564-1565) epingle aLevel1 a 145: portes equivalentes.
    public const int MinimumAttackerBaseLevel = 113;

    public static int ComputeGain(int attackerBaseLevel, int petExpRatio = DefaultPetExpRatio)
    {
        return attackerBaseLevel < MinimumAttackerBaseLevel ? 0 : petExpRatio;
    }
}

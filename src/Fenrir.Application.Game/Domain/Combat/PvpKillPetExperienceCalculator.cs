namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillPetExperienceCalculator
{
    // ServerInfo.ini [Zone.Server] KillOtherTribePetExpRatio, recopie dans mGAME.mKillOtherTribePetExpRatio.
    // Server/BuildEU33/ServerInfo.ini:176, Server/ts25zone/S07_MyGame03.cpp:2972
    public const int DefaultPetExpRatio = 200;

    // LNW33 coupe le gain sous LV_M1 sur aLevel1 seul; aLevel2 > 0 implique aLevel1 == 145,
    // donc le niveau combine est equivalent. Server/ts25zone/S07_MyGame03.cpp:2974
    private const int LevelFloor = ExperienceFormulas.RebirthDivisorLevelThreshold;

    public static int ComputeGain(int attackerCombinedLevel, int petExpRatio = DefaultPetExpRatio)
    {
        return attackerCombinedLevel < LevelFloor ? 0 : petExpRatio;
    }
}

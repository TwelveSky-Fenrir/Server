namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountKillExperienceCalculator
{
    // Server/BuildEU33/ServerInfo.ini:160 MountExpUpRatio, lu brut (S07_MyGame01.cpp:435).
    // Forfait par kill: Server/ts25zone/S07_MyGame03.cpp:2993 l'affecte sans le multiplier a l'EXP joueur.
    public const int DefaultBaseExperiencePerKill = 40;

    public static int ComputeGain(
        bool isMounted, int mountActivity, int mountExperience,
        bool hasDoubleExp, bool hasSessionExpUp,
        int baseAmount = DefaultBaseExperiencePerKill)
    {
        if (!isMounted || mountActivity <= 0 || mountExperience >= MountActivityExpCodec.MaxExp)
            return 0;

        var amount = baseAmount;
        if (hasDoubleExp)
            amount *= 2;
        if (hasSessionExpUp)
            amount *= 2;
        return amount;
    }
}

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountKillExperienceCalculator
{
    // ServerInfo.ini [Zone.Server] MountExpUpRatio, recopie dans mGAME.mMountExpUpRatio.
    // Server/BuildEU33/ServerInfo.ini:160, Server/ts25zone/S07_MyGame03.cpp:2993
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

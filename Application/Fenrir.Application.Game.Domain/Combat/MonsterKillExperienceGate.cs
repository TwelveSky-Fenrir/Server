namespace Fenrir.Application.Game.Domain.Combat;

public static class MonsterKillExperienceGate
{

        public static bool ShouldProcess(bool isReady, bool isTransferringZone, int characterExperienceAmount,
        int petExperienceAmount)
    {
        if (!isReady || isTransferringZone)
            return false;

        return characterExperienceAmount >= 1 || petExperienceAmount >= 1;
    }
}

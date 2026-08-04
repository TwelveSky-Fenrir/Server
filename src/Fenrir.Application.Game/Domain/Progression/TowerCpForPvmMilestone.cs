using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Progression;

public static class TowerCpForPvmMilestone
{
    public const int KillsRequired = 1000;

    public const int BaseKillCp = 1;

    public const int ExtinctionScrollKillCp = 4;

    public const int PremiumKillCpBonus = 2;

    public const int HeroRankPointsPerMilestone = 1;

    public static KillRegistration RegisterKill(int counterBeforeThisKill, int attackerLevel1, int attackerLevel2,
        int monsterRealLevel)
    {
        var fixedLevel = ExperienceFormulas.ReturnFixedLevel(attackerLevel1 + attackerLevel2);
        if (fixedLevel - monsterRealLevel >= 10)
            return new KillRegistration(counterBeforeThisKill, false);

        var next = counterBeforeThisKill + 1;
        return next >= KillsRequired
            ? new KillRegistration(0, true)
            : new KillRegistration(next, false);
    }

    public static int ComputeReward(int cpForPvmTowerBonus, bool extinctionScrollCharged = false,
        bool premiumActive = false)
    {
        var killCp = extinctionScrollCharged ? ExtinctionScrollKillCp : BaseKillCp;
        if (premiumActive)
            killCp += PremiumKillCpBonus;

        return killCp + Math.Max(0, cpForPvmTowerBonus);
    }

    public readonly record struct KillRegistration(int UpdatedCounter, bool MilestoneReached);
}

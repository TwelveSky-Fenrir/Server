namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillContributionPointCalculator
{
    public const int BasePerKillAmount = 2;

    public const int PremiumStatusBonus = 2;

    public const int WarriorScrollBuffBonus = 1;

    // MAX_NUMBER_SIZE, plafond dur applique par getMaxCP a chaque octroi de CP.
    // Server/Header/Protocol/DEFINE.h:363, Server/ts25zone/S07_MyGame03.cpp:2345
    public const int ContributionPointHardCap = 2_000_000_000;

    public const int FfaOverrideFlatAmount = 20;

    public const int RegularWarOverrideFlatCpAmount = 20;

    public const int RegularWarOverrideWarPointAmount = 2;

    public const int RegularWarOverrideBloodPointAmount = 2;

    public static readonly TimeSpan FlatOverrideCooldown = TimeSpan.FromMinutes(2);

    public static int ComputeBaseAmount(
        bool hasPremiumStatus,
        bool hasWarriorScrollBuff,
        int perCharacterOverride = 0,
        int perTribeWorldStateBonus = 0,
        int towerControlBonus = 0,
        int basePerKillAmount = BasePerKillAmount)
    {
        var amount = basePerKillAmount + perCharacterOverride + perTribeWorldStateBonus + towerControlBonus;

        if (hasPremiumStatus)
            amount += PremiumStatusBonus;
        if (hasWarriorScrollBuff)
            amount += WarriorScrollBuffBonus;

        return amount;
    }

    public static int ClampGrant(int currentTotal, int amountToAdd, int cap)
    {
        return Math.Min(amountToAdd, cap - currentTotal);
    }
}

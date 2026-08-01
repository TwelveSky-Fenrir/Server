namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillContributionPointCalculator
{
    // Server/ts25zone/S07_MyGame03.cpp:2502-2503
    public const int PremiumStatusBonus = 2;

    // Server/ts25zone/S07_MyGame03.cpp:2505-2506
    public const int WarriorScrollBuffBonus = 1;

    // Server/Header/Protocol/DEFINE.h:363 MAX_NUMBER_SIZE, le plafond que getMaxCP oppose au credit.
    public const int ContributionPointHardCap = 2_000_000_000;

    public const int FfaOverrideFlatAmount = 20;

    public const int RegularWarOverrideFlatCpAmount = 20;

    public const int RegularWarOverrideWarPointAmount = 2;

    public const int RegularWarOverrideBloodPointAmount = 2;

    public static readonly TimeSpan FlatOverrideCooldown = TimeSpan.FromMinutes(2);

    // Server/ts25zone/S07_MyGame03.cpp:2496-2506 compose tCPAddNum1: additif jeu + additif joueur +
    // additif tribu + tour, puis +2 premium et +1 parchemin. Aucun repli code-cote ici.
    public static int ComputeBaseAmount(
        bool hasPremiumStatus,
        bool hasWarriorScrollBuff,
        int perCharacterOverride,
        int perTribeWorldStateBonus,
        int towerControlBonus,
        int basePerKillAmount)
    {
        var amount = basePerKillAmount + perCharacterOverride + perTribeWorldStateBonus + towerControlBonus;

        if (hasPremiumStatus)
            amount += PremiumStatusBonus;
        if (hasWarriorScrollBuff)
            amount += WarriorScrollBuffBonus;

        return amount;
    }

    // Server/ts25zone/S07_MyGame03.cpp:2345-2353 getMaxCP elargit en LONGLONG; :2874 n'accorde le credit
    // que si le reste est > 0. Aucun appelant C# ne porte cette garde: elle doit vivre ici.
    public static int ClampGrant(int currentTotal, int amountToAdd, int cap)
    {
        var grant = Math.Min(amountToAdd, cap - (long)currentTotal);
        return grant > 0 ? (int)grant : 0;
    }
}

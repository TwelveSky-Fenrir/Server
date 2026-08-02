namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillContributionPointCalculator
{
    public const int PremiumStatusBonus = 2;

    public const int WarriorScrollBuffBonus = 1;

    /// <summary>
    ///     Extra CP added per PvP kill when <c>DoubleKillNumTime &gt; 0</c> (Scroll of Loyalty / Scroll of the Gods).
    ///     TODO(fenrir-gameplay-domain-engineer): Exact legacy value not confirmed from contract.
    ///     Verify the bonus amount from Server/ts25zone/S07_MyGame02.cpp B_AVATAR_CHANGE_INFO_2 or
    ///     the aDoubleKillNumTime branch before shipping. Using the same +1 as WarriorScrollBuffBonus as a
    ///     placeholder — replace if the actual legacy value differs.
    ///     Réf. legacy: aDoubleKillNumTime check — Server/ts25zone/S07_MyGame02.cpp.
    /// </summary>
    public const int DoubleKillNumTimeBuff = 1;

    public const int ContributionPointHardCap = 2_000_000_000;

    public const int FfaOverrideFlatAmount = 20;

    public const int RegularWarOverrideFlatCpAmount = 20;

    public const int RegularWarOverrideWarPointAmount = 2;

    public const int RegularWarOverrideBloodPointAmount = 2;

    public static readonly TimeSpan FlatOverrideCooldown = TimeSpan.FromMinutes(2);

    public static int ComputeBaseAmount(
        bool hasPremiumStatus,
        bool hasWarriorScrollBuff,
        bool hasDoubleKillNumTimeBuff,
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
        if (hasDoubleKillNumTimeBuff)
            amount += DoubleKillNumTimeBuff;

        return amount;
    }

    public static int ClampGrant(int currentTotal, int amountToAdd, int cap)
    {
        var grant = Math.Min(amountToAdd, cap - (long)currentTotal);
        return grant > 0 ? (int)grant : 0;
    }
}

namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillContributionPointCalculator
{
    public const int PremiumStatusBonus = 2;

    public const int WarriorScrollBuffBonus = 1;

    public const int ContributionPointHardCap = 2_000_000_000;

    public const int FfaOverrideFlatAmount = 20;

    public const int RegularWarOverrideFlatCpAmount = 20;

    public const int RegularWarOverrideWarPointAmount = 2;

    public const int RegularWarOverrideBloodPointAmount = 2;

    public static readonly TimeSpan FlatOverrideCooldown = TimeSpan.FromMinutes(2);

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

    public static int ClampGrant(int currentTotal, int amountToAdd, int cap)
    {
        var grant = Math.Min(amountToAdd, cap - (long)currentTotal);
        return grant > 0 ? (int)grant : 0;
    }

    /// <summary>
    ///     Resolves the CP double-award: while <c>DoubleKillNumTime</c> is still positive after the kill's
    ///     first grant, the identical base amount is applied a second time, independently clamped against
    ///     <paramref name="totalAfterFirstGrant" />. Not a flat bonus and not a ×2 multiplier — the whole
    ///     per-kill CP figure is granted twice, each half capped on its own.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:2871-2893 (double-award block) and :2345-2353
    ///     (<c>getMaxCP</c>, the clamp underlying both grants).
    /// </remarks>
    public static DoubleAwardResult ResolveDoubleAward(int totalAfterFirstGrant, int baseAmount, int cap,
        int doubleKillNumTime)
    {
        if (doubleKillNumTime <= 0)
            return default;

        var grant = ClampGrant(totalAfterFirstGrant, baseAmount, cap);
        return grant > 0 ? new DoubleAwardResult(true, grant, doubleKillNumTime - 1) : default;
    }

    /// <summary>Outcome of <see cref="ResolveDoubleAward" />; <see cref="GrantedAmount" />/<see cref="NewDoubleKillNumTime" /> are meaningful only when <see cref="Fired" />.</summary>
    public readonly record struct DoubleAwardResult(bool Fired, int GrantedAmount, int NewDoubleKillNumTime);
}

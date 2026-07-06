using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class PvpKillContributionPointCalculatorTests
{
    [Fact]
    public void ComputeBaseAmount_NoBonuses_ReturnsBaseOnly()
    {
        var amount = PvpKillContributionPointCalculator.ComputeBaseAmount(
            false, false);

        Assert.Equal(PvpKillContributionPointCalculator.BasePerKillAmount, amount);
    }

    [Fact]
    public void ComputeBaseAmount_Premium_AddsPremiumBonus()
    {
        var amount = PvpKillContributionPointCalculator.ComputeBaseAmount(
            true, false);

        Assert.Equal(PvpKillContributionPointCalculator.BasePerKillAmount +
                     PvpKillContributionPointCalculator.PremiumStatusBonus, amount);
    }

    [Fact]
    public void ComputeBaseAmount_WarriorScroll_AddsWarriorScrollBonus()
    {
        var amount = PvpKillContributionPointCalculator.ComputeBaseAmount(
            false, true);

        Assert.Equal(PvpKillContributionPointCalculator.BasePerKillAmount +
                     PvpKillContributionPointCalculator.WarriorScrollBuffBonus, amount);
    }

    [Fact]
    public void ComputeBaseAmount_BothBonusesAndOverrides_SumsEveryTerm()
    {
        var amount = PvpKillContributionPointCalculator.ComputeBaseAmount(
            true, true,
            3, 4, 5,
            10);

        Assert.Equal(10 + 3 + 4 + 5 +
                     PvpKillContributionPointCalculator.PremiumStatusBonus +
                     PvpKillContributionPointCalculator.WarriorScrollBuffBonus, amount);
    }

    [Fact]
    public void ClampGrant_WellBelowCap_ReturnsFullAmount()
    {
        var granted = PvpKillContributionPointCalculator.ClampGrant(0, 10, 1000);

        Assert.Equal(10, granted);
    }

    [Fact]
    public void ClampGrant_WouldOvershootCap_ReturnsReducedAmount()
    {
        var granted = PvpKillContributionPointCalculator.ClampGrant(995, 10, 1000);

        Assert.Equal(5, granted);
    }

    [Fact]
    public void ClampGrant_AlreadyPastCap_ReturnsNegative()
    {
        var granted = PvpKillContributionPointCalculator.ClampGrant(1010, 10, 1000);

        Assert.Equal(-10, granted);
    }

    [Fact]
    public void ClampGrant_ExactlyAtCap_ReturnsZero()
    {
        var granted = PvpKillContributionPointCalculator.ClampGrant(1000, 10, 1000);

        Assert.Equal(0, granted);
    }
}

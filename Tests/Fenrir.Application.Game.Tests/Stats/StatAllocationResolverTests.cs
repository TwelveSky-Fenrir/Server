using Fenrir.Application.Game.Domain.Stats;

namespace Fenrir.Application.Game.Tests.Stats;

// tSort 206, ProcessForStatPlus (S04_MyWork05.cpp:705-791) -- three affordability regimes over category codes
// 1-12, mapped Strength/Dexterity/Vitality/Intelligence in that order for every regime.
public class StatAllocationResolverTests
{
    [Theory]
    [InlineData(1, StatAllocationResolver.BaseStat.Strength)]
    [InlineData(2, StatAllocationResolver.BaseStat.Dexterity)]
    [InlineData(3, StatAllocationResolver.BaseStat.Vitality)]
    [InlineData(4, StatAllocationResolver.BaseStat.Intelligence)]
    public void SmallFixedCategory_Succeeds_CreditsOneAndDebitsOne(int statSort, StatAllocationResolver.BaseStat stat)
    {
        var result = StatAllocationResolver.Resolve(statSort, addValue: 0, currentStatPoints: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(stat, result.Stat);
        Assert.Equal(1, result.Amount);
        Assert.Equal(0, result.NewStatPoints);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SmallFixedCategory_ZeroBalance_Disconnects_DoesNotMutate(int statSort)
    {
        var result = StatAllocationResolver.Resolve(statSort, addValue: 0, currentStatPoints: 0);

        Assert.False(result.Succeeded);
        Assert.Equal(StatAllocationResolver.Outcome.Disconnect, result.Outcome);
    }

    /// <summary>tAddValue is present on the wire for categories 1-4 but must have zero effect on the outcome.</summary>
    [Fact]
    public void SmallFixedCategory_AddValueIgnored()
    {
        var withZero = StatAllocationResolver.Resolve(1, addValue: 0, currentStatPoints: 10);
        var withHuge = StatAllocationResolver.Resolve(1, addValue: 999_999, currentStatPoints: 10);
        var withNegative = StatAllocationResolver.Resolve(1, addValue: -5, currentStatPoints: 10);

        Assert.Equal(withZero, withHuge);
        Assert.Equal(withZero, withNegative);
        Assert.Equal(1, withZero.Amount);
        Assert.Equal(9, withZero.NewStatPoints);
    }

    [Theory]
    [InlineData(5, StatAllocationResolver.BaseStat.Strength)]
    [InlineData(6, StatAllocationResolver.BaseStat.Dexterity)]
    [InlineData(7, StatAllocationResolver.BaseStat.Vitality)]
    [InlineData(8, StatAllocationResolver.BaseStat.Intelligence)]
    public void LargeFixedCategory_Succeeds_CreditsFiveAndDebitsFive(int statSort,
        StatAllocationResolver.BaseStat stat)
    {
        var result = StatAllocationResolver.Resolve(statSort, addValue: 0, currentStatPoints: 5);

        Assert.True(result.Succeeded);
        Assert.Equal(stat, result.Stat);
        Assert.Equal(5, result.Amount);
        Assert.Equal(0, result.NewStatPoints);
    }

    [Fact]
    public void LargeFixedCategory_ExactlyFourBelowThreshold_Disconnects()
    {
        var result = StatAllocationResolver.Resolve(5, addValue: 0, currentStatPoints: 4);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void LargeFixedCategory_NoPartialGrant_WhenUnaffordable()
    {
        // The server never grants "as many as the character can afford" -- confirms the whole request rejects,
        // not a reduced-amount success.
        var result = StatAllocationResolver.Resolve(8, addValue: 0, currentStatPoints: 4);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.Amount);
    }

    [Theory]
    [InlineData(9, StatAllocationResolver.BaseStat.Strength)]
    [InlineData(10, StatAllocationResolver.BaseStat.Dexterity)]
    [InlineData(11, StatAllocationResolver.BaseStat.Vitality)]
    [InlineData(12, StatAllocationResolver.BaseStat.Intelligence)]
    public void VariableCategory_Succeeds_CreditsAndDebitsTheRequestedAmount(int statSort,
        StatAllocationResolver.BaseStat stat)
    {
        var result = StatAllocationResolver.Resolve(statSort, addValue: 37, currentStatPoints: 100);

        Assert.True(result.Succeeded);
        Assert.Equal(stat, result.Stat);
        Assert.Equal(37, result.Amount);
        Assert.Equal(63, result.NewStatPoints);
    }

    [Fact]
    public void VariableCategory_ExactBalance_Succeeds_ZerosBalance()
    {
        var result = StatAllocationResolver.Resolve(9, addValue: 100, currentStatPoints: 100);

        Assert.True(result.Succeeded);
        Assert.Equal(100, result.Amount);
        Assert.Equal(0, result.NewStatPoints);
    }

    [Fact]
    public void VariableCategory_AmountAboveBalance_Disconnects()
    {
        var result = StatAllocationResolver.Resolve(9, addValue: 101, currentStatPoints: 100);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void VariableCategory_AmountBelowOne_Disconnects(int addValue)
    {
        var result = StatAllocationResolver.Resolve(9, addValue, currentStatPoints: 100);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(1000)]
    public void CategoryOutsideLegalRange_Disconnects_RegardlessOfBalance(int statSort)
    {
        var result = StatAllocationResolver.Resolve(statSort, addValue: 50, currentStatPoints: 1_000_000);

        Assert.False(result.Succeeded);
        Assert.Equal(StatAllocationResolver.Outcome.Disconnect, result.Outcome);
    }
}

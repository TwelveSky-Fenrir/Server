using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Simulation;

public class AutoHuntBudgetPolicyTests
{
    private const int OneMinuteTicks = SimulationClock.PlayTimeAccrualLegacyTicks;
    private const int Today = 20_260_710;

    [Fact]
    public void BothTiersZero_IsAClean_NoOp_NeverExhausted()
    {
        var result = AutoHuntBudgetPolicy.Advance(0, 0, 0, 1, Today);

        Assert.Equal(AutoHuntBudgetPolicy.Signal.None, result.Signal);
        Assert.Equal(0, result.DayBudget);
        Assert.Equal(0, result.MinuteBudget);
    }

    [Fact]
    public void DayBudgetPresentAndNotExpired_NoOp_MinuteTierUntouched()
    {
        var result = AutoHuntBudgetPolicy.Advance(20_260_711, 5, OneMinuteTicks - 1, OneMinuteTicks * 10, Today);

        Assert.Equal(AutoHuntBudgetPolicy.Signal.None, result.Signal);
        Assert.Equal(20_260_711, result.DayBudget);
        Assert.Equal(5, result.MinuteBudget);
        Assert.Equal(OneMinuteTicks - 1, result.MinuteAccrualTicks);
    }

    [Fact]
    public void DayBudgetExpired_WithMinuteRemaining_ZeroesDay_KeepsMinute()
    {
        var result = AutoHuntBudgetPolicy.Advance(20_000_101, 5, 0, 1, Today);

        Assert.Equal(AutoHuntBudgetPolicy.Signal.DayBudgetExpired, result.Signal);
        Assert.Equal(0, result.DayBudget);
        Assert.Equal(5, result.MinuteBudget);
    }

    [Fact]
    public void DayBudgetExpired_WithNoMinute_IsExhausted()
    {
        var result = AutoHuntBudgetPolicy.Advance(20_000_101, 0, 0, 1, Today);

        Assert.Equal(AutoHuntBudgetPolicy.Signal.Exhausted, result.Signal);
        Assert.Equal(0, result.DayBudget);
        Assert.Equal(0, result.MinuteBudget);
    }

    [Fact]
    public void MinuteTier_LessThanAWholeMinuteElapsed_AccruesOnly()
    {
        var result = AutoHuntBudgetPolicy.Advance(0, 5, 0, OneMinuteTicks - 1, Today);

        Assert.Equal(AutoHuntBudgetPolicy.Signal.None, result.Signal);
        Assert.Equal(5, result.MinuteBudget);
        Assert.Equal(OneMinuteTicks - 1, result.MinuteAccrualTicks);
    }

    [Fact]
    public void MinuteTier_OneWholeMinute_DecrementsByOne_CarriesRemainder()
    {
        var result = AutoHuntBudgetPolicy.Advance(0, 5, 10, OneMinuteTicks, Today);

        Assert.Equal(AutoHuntBudgetPolicy.Signal.MinuteBudgetDecremented, result.Signal);
        Assert.Equal(4, result.MinuteBudget);
        Assert.Equal(10, result.MinuteAccrualTicks);
    }

    [Fact]
    public void MinuteTier_LastMinute_IsExhausted()
    {
        var result = AutoHuntBudgetPolicy.Advance(0, 1, 0, OneMinuteTicks, Today);

        Assert.Equal(AutoHuntBudgetPolicy.Signal.Exhausted, result.Signal);
        Assert.Equal(0, result.MinuteBudget);
    }

    [Fact]
    public void MinuteTier_BurstOfManyMinutes_CatchesUpTheWholeAmount()
    {
        var result = AutoHuntBudgetPolicy.Advance(0, 10, 0, OneMinuteTicks * 3, Today);

        Assert.Equal(AutoHuntBudgetPolicy.Signal.MinuteBudgetDecremented, result.Signal);
        Assert.Equal(7, result.MinuteBudget);
        Assert.Equal(0, result.MinuteAccrualTicks);
    }

    [Fact]
    public void MinuteTier_BurstExceedingRemaining_FloorsAtZero_AndExhausts()
    {
        var result = AutoHuntBudgetPolicy.Advance(0, 2, 0, OneMinuteTicks * 5, Today);

        Assert.Equal(AutoHuntBudgetPolicy.Signal.Exhausted, result.Signal);
        Assert.Equal(0, result.MinuteBudget);
    }
}

using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Tests.Consumables;

public class CashTimerResolverTests
{
    [Fact]
    public void ResolveFactionNotice_AddsFiveToTheBankedCount()
    {
        var result = CashTimerResolver.ResolveFactionNotice(2);

        Assert.True(result.Succeeded);
        Assert.Equal(7, result.NewValue);
    }

    [Fact]
    public void ResolveFactionNotice_WouldExceedCeiling_Rejects()
    {
        var result = CashTimerResolver.ResolveFactionNotice(BankedCounterMath.GlobalCeiling);

        Assert.Equal(CashTimerResolver.Outcome.WouldExceedCeiling, result.Outcome);
    }

    [Fact]
    public void ResolveTaiyanKey_AtLevelCap_AddsOneHundredEighty()
    {
        var result = CashTimerResolver.ResolveTaiyanKey(LevelProgressionCalculator.MaxLevel, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(180, result.NewValue);
    }

    [Fact]
    public void ResolveTaiyanKey_BelowLevelCap_ReportsLevelCapNotMet_ForTheHandlerToDisconnect()
    {
        var result = CashTimerResolver.ResolveTaiyanKey(LevelProgressionCalculator.MaxLevel - 1, 0);

        Assert.Equal(CashTimerResolver.Outcome.LevelCapNotMet, result.Outcome);
    }

    [Fact]
    public void ResolveTaiyanKey_WouldExceedCeiling_Rejects()
    {
        var result = CashTimerResolver.ResolveTaiyanKey(LevelProgressionCalculator.MaxLevel,
            BankedCounterMath.GlobalCeiling);

        Assert.Equal(CashTimerResolver.Outcome.WouldExceedCeiling, result.Outcome);
    }
}

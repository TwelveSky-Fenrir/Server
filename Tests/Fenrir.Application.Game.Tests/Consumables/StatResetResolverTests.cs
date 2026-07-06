using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class StatResetResolverTests
{
    [Theory]
    [InlineData(1, 0, StatResetResolver.LevelBand.UpTo99)]
    [InlineData(99, 0, StatResetResolver.LevelBand.UpTo99)]
    [InlineData(100, 0, StatResetResolver.LevelBand.Level100To112)]
    [InlineData(112, 0, StatResetResolver.LevelBand.Level100To112)]
    [InlineData(113, 0, StatResetResolver.LevelBand.Level113PlusNoRebirth)]
    [InlineData(144, 0, StatResetResolver.LevelBand.Level113PlusNoRebirth)]
    [InlineData(145, 1, StatResetResolver.LevelBand.Level145PlusWithRebirth)]
    [InlineData(200, 3, StatResetResolver.LevelBand.Level145PlusWithRebirth)]
    public void TryResolveLevelBand_UnambiguousRanges_ResolveTheExpectedBand(short level, int rebirthCount,
        StatResetResolver.LevelBand expected)
    {
        Assert.True(StatResetResolver.TryResolveLevelBand(level, rebirthCount, out var band));
        Assert.Equal(expected, band);
    }

    [Fact]
    public void TryResolveLevelBand_AmbiguousMidRange_ReturnsFalse_RatherThanGuessing()
    {
        // Base level 113-144 with at least one rebirth: not unambiguously covered by either citation.
        Assert.False(StatResetResolver.TryResolveLevelBand(120, 1, out _));
        Assert.False(StatResetResolver.TryResolveLevelBand(113, 2, out _));
        Assert.False(StatResetResolver.TryResolveLevelBand(144, 1, out _));
    }

    [Fact]
    public void ResolveStatsClear_RefundsEverythingAboveFloor_AndResetsAllFourToFloor()
    {
        var result = StatResetResolver.ResolveStatsClear(statVit: 10, statStr: 25, statInt: 1, statDex: 4);

        Assert.Equal(1, result.NewStatVit);
        Assert.Equal(1, result.NewStatStr);
        Assert.Equal(1, result.NewStatInt);
        Assert.Equal(1, result.NewStatDex);
        // (10-1) + (25-1) + (1-1) + (4-1) = 9 + 24 + 0 + 3 = 36
        Assert.Equal(36, result.RefundedPoints);
    }

    [Fact]
    public void ResolveStatsClear_AllAlreadyAtFloor_RefundsZero()
    {
        var result = StatResetResolver.ResolveStatsClear(1, 1, 1, 1);

        Assert.Equal(0, result.RefundedPoints);
    }

    [Fact]
    public void ResolveStatCleanse_AboveFloor_RefundsTheDifference_AndResetsToFloor()
    {
        var result = StatResetResolver.ResolveStatCleanse(currentValue: 15);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.NewValue);
        Assert.Equal(14, result.RefundedPoints);
    }

    [Fact]
    public void ResolveStatCleanse_AlreadyAtFloor_FailsCleanly_WithNoRefund()
    {
        var result = StatResetResolver.ResolveStatCleanse(currentValue: 1);

        Assert.Equal(StatResetResolver.CleanseOutcome.AlreadyAtFloor, result.Outcome);
        Assert.Equal(0, result.RefundedPoints);
    }
}

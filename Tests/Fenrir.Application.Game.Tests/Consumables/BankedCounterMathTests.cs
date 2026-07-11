using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class BankedCounterMathTests
{
    [Fact]
    public void AddWideSafe_WithinCeiling_Succeeds()
    {
        var result = BankedCounterMath.AddWideSafe(10, 5);

        Assert.True(result.Succeeded);
        Assert.Equal(15, result.NewValue);
    }

    [Fact]
    public void AddWideSafe_WouldExceedCeiling_Rejects_AndLeavesCurrentUnchanged()
    {
        var result = BankedCounterMath.AddWideSafe(BankedCounterMath.GlobalCeiling - 2, 5);

        Assert.Equal(BankedCounterMath.AddOutcome.WouldExceedCeiling, result.Outcome);
        Assert.Equal(BankedCounterMath.GlobalCeiling - 2, result.NewValue);
    }

    [Fact]
    public void AddWideSafe_ExactlyAtCeiling_Succeeds()
    {
        var result = BankedCounterMath.AddWideSafe(BankedCounterMath.GlobalCeiling - 5, 5);

        Assert.True(result.Succeeded);
        Assert.Equal(BankedCounterMath.GlobalCeiling, result.NewValue);
    }

    [Fact]
    public void AddWideSafe_LargeBulkAmount_NeverOverflowsBeforeTheCeilingCompare()
    {
        var result = BankedCounterMath.AddWideSafe(int.MaxValue - 10, 999);

        Assert.Equal(BankedCounterMath.AddOutcome.WouldExceedCeiling, result.Outcome);
    }

    [Fact]
    public void AddNarrow_WithinCeiling_Succeeds()
    {
        var result = BankedCounterMath.AddNarrow(10, 5);

        Assert.True(result.Succeeded);
        Assert.Equal(15, result.NewValue);
    }

    [Fact]
    public void AddNarrow_WouldExceedCeiling_Rejects()
    {
        var result = BankedCounterMath.AddNarrow(BankedCounterMath.GlobalCeiling, 1);

        Assert.Equal(BankedCounterMath.AddOutcome.WouldExceedCeiling, result.Outcome);
        Assert.Equal(BankedCounterMath.GlobalCeiling, result.NewValue);
    }

    [Fact]
    public void CoerceBulkToHeadroom_RequestFitsEntirely_ReturnsFullRequest()
    {
        var count = BankedCounterMath.CoerceBulkToHeadroom(0, 200, 10, 5);

        Assert.Equal(5, count);
    }

    [Fact]
    public void CoerceBulkToHeadroom_RequestOvershootsCap_IsSilentlyReducedToWhatFits()
    {
        var count = BankedCounterMath.CoerceBulkToHeadroom(190, 200, 10, 5);

        Assert.Equal(1, count);
    }

    [Fact]
    public void CoerceBulkToHeadroom_AlreadyAtCap_ReturnsZero()
    {
        var count = BankedCounterMath.CoerceBulkToHeadroom(200, 200, 10, 5);

        Assert.Equal(0, count);
    }

    [Fact]
    public void CoerceBulkToHeadroom_NonPositiveInputs_ReturnZero()
    {
        Assert.Equal(0, BankedCounterMath.CoerceBulkToHeadroom(0, 200, 0, 5));
        Assert.Equal(0, BankedCounterMath.CoerceBulkToHeadroom(0, 200, 10, 0));
    }
}

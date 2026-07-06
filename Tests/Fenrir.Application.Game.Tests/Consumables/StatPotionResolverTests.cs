using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class StatPotionResolverTests
{
    [Fact]
    public void ResolveOrdinary_SingleUse_AddsThePerUnitAmount()
    {
        var result = StatPotionResolver.ResolveOrdinary(0, 1, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.NewCount);
        Assert.Equal(1, result.UnitsConsumed);
    }

    [Fact]
    public void ResolveOrdinary_BulkRequestFitsEntirely_ConsumesTheFullRequest()
    {
        var result = StatPotionResolver.ResolveOrdinary(0, 10, 5);

        Assert.True(result.Succeeded);
        Assert.Equal(50, result.NewCount);
        Assert.Equal(5, result.UnitsConsumed);
    }

    [Fact]
    public void ResolveOrdinary_BulkRequestOvershootsCap_IsSilentlyReducedToWhatFits()
    {
        var result = StatPotionResolver.ResolveOrdinary(195, 1, 10);

        Assert.True(result.Succeeded);
        Assert.Equal(StatPotionResolver.OrdinaryTierCap, result.NewCount);
        Assert.Equal(5, result.UnitsConsumed);
    }

    [Fact]
    public void ResolveOrdinary_AlreadyAtCap_ReportsAlreadyMaxed_NotThePlainFailure()
    {
        var result = StatPotionResolver.ResolveOrdinary(StatPotionResolver.OrdinaryTierCap, 1,
            1);

        Assert.Equal(StatPotionResolver.Outcome.AlreadyMaxed, result.Outcome);
        Assert.Equal(0, result.UnitsConsumed);
    }

    [Fact]
    public void ResolveG12_PreconditionsMet_AddsExactlyOneUnit_RegardlessOfBulk()
    {
        var result = StatPotionResolver.ResolveG12(StatPotionResolver.OrdinaryTierCap, 5,
            3);

        Assert.True(result.Succeeded);
        Assert.Equal(StatPotionResolver.OrdinaryTierCap + 1, result.NewCount);
    }

    [Fact]
    public void ResolveG12_BelowOrdinaryCap_FailsPrecondition()
    {
        var result = StatPotionResolver.ResolveG12(StatPotionResolver.OrdinaryTierCap - 1,
            5, 3);

        Assert.Equal(StatPotionResolver.Outcome.PreconditionFailed, result.Outcome);
    }

    [Fact]
    public void ResolveG12_AlreadyAtG12Cap_FailsPrecondition()
    {
        var result = StatPotionResolver.ResolveG12(StatPotionResolver.G12TierCap, 5,
            3);

        Assert.Equal(StatPotionResolver.Outcome.PreconditionFailed, result.Outcome);
    }

    [Fact]
    public void ResolveG12_RebirthBelowRequiredThreshold_FailsPrecondition()
    {
        var result = StatPotionResolver.ResolveG12(StatPotionResolver.OrdinaryTierCap, 1,
            3);

        Assert.Equal(StatPotionResolver.Outcome.PreconditionFailed, result.Outcome);
    }
}

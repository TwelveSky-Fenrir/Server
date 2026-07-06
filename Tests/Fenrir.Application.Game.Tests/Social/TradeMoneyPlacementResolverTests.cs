using Fenrir.Application.Game.Domain.Social.Trade;

namespace Fenrir.Application.Game.Tests.Social;

public class TradeMoneyPlacementResolverTests
{
    // ---- tSort 221 -- ResolveToTradeOffer ----

    [Fact]
    public void ResolveToTradeOffer_AmountBelowOne_IsQuantityOutOfRange()
    {
        var result = TradeMoneyPlacementResolver.ResolveToTradeOffer(1000, 0, 0);

        Assert.Equal(TradeMoneyPlacementResolver.MoneyPlacementOutcome.QuantityOutOfRange, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveToTradeOffer_AmountExceedsOnHandBalance_IsInsufficientSourceBalance()
    {
        var result = TradeMoneyPlacementResolver.ResolveToTradeOffer(500, 0, 501);

        Assert.Equal(TradeMoneyPlacementResolver.MoneyPlacementOutcome.InsufficientSourceBalance, result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_ResultingTradeOfferExceedsCeiling_IsDestinationOverflow()
    {
        var result = TradeMoneyPlacementResolver.ResolveToTradeOffer(
            onHandMoney: TradeMoneyPlacementResolver.MoneyCeiling,
            tradeOfferMoney: TradeMoneyPlacementResolver.MoneyCeiling,
            amount: 1);

        Assert.Equal(TradeMoneyPlacementResolver.MoneyPlacementOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_ResultingTradeOfferExactlyAtCeiling_Succeeds()
    {
        var result = TradeMoneyPlacementResolver.ResolveToTradeOffer(
            onHandMoney: TradeMoneyPlacementResolver.MoneyCeiling,
            tradeOfferMoney: 0,
            amount: TradeMoneyPlacementResolver.MoneyCeiling);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NewOnHandMoney);
        Assert.Equal(TradeMoneyPlacementResolver.MoneyCeiling, result.NewTradeOfferMoney);
    }

    [Fact]
    public void ResolveToTradeOffer_Success_MovesAmountBetweenBothBalances()
    {
        var result = TradeMoneyPlacementResolver.ResolveToTradeOffer(1000, 200, 300);

        Assert.True(result.Succeeded);
        Assert.Equal(700, result.NewOnHandMoney);
        Assert.Equal(500, result.NewTradeOfferMoney);
    }

    // ---- tSort 222 -- ResolveFromTradeOffer ----

    [Fact]
    public void ResolveFromTradeOffer_AmountBelowOne_IsQuantityOutOfRange()
    {
        var result = TradeMoneyPlacementResolver.ResolveFromTradeOffer(1000, 0, 0);

        Assert.Equal(TradeMoneyPlacementResolver.MoneyPlacementOutcome.QuantityOutOfRange, result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_AmountExceedsTradeOfferBalance_IsInsufficientSourceBalance()
    {
        var result = TradeMoneyPlacementResolver.ResolveFromTradeOffer(500, 0, 501);

        Assert.Equal(TradeMoneyPlacementResolver.MoneyPlacementOutcome.InsufficientSourceBalance, result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_ResultingOnHandExceedsCeiling_IsDestinationOverflow()
    {
        var result = TradeMoneyPlacementResolver.ResolveFromTradeOffer(
            tradeOfferMoney: TradeMoneyPlacementResolver.MoneyCeiling,
            onHandMoney: TradeMoneyPlacementResolver.MoneyCeiling,
            amount: 1);

        Assert.Equal(TradeMoneyPlacementResolver.MoneyPlacementOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_Success_MovesAmountBetweenBothBalances()
    {
        var result = TradeMoneyPlacementResolver.ResolveFromTradeOffer(500, 1000, 300);

        Assert.True(result.Succeeded);
        Assert.Equal(1300, result.NewOnHandMoney);
        Assert.Equal(200, result.NewTradeOfferMoney);
    }

    [Fact]
    public void ResolveFromTradeOffer_DoesNotTouchBigMoney_OnlyReturnsNormalMoneyBalances()
    {
        // Purely a documentation-style assertion: the result shape has no BigMoney field at all, so a caller
        // physically cannot route tSort 221/222 amounts into TradeOfferSide.BigMoney by mistake.
        var result = TradeMoneyPlacementResolver.ResolveToTradeOffer(100, 0, 50);

        Assert.True(result.Succeeded);
    }
}

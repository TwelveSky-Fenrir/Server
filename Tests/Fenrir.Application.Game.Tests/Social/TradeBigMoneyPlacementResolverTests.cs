using Fenrir.Application.Game.Domain.Social.Trade;

namespace Fenrir.Application.Game.Tests.Social;

/// <summary>
///     Coverage for <see cref="TradeBigMoneyPlacementResolver" />, the pure policy behind tSort 240
///     (Inventory -&gt; Trade BigMoney) and 243 (Trade -&gt; Inventory BigMoney), including the trade-lock guard
///     unique to this pair. Does not depend on any dispatch/trade-registry wiring.
/// </summary>
public class TradeBigMoneyPlacementResolverTests
{
    // ---- tSort 240 -- ResolveToTradeOffer ----

    [Fact]
    public void ResolveToTradeOffer_NeitherSideConfirmed_IsNotLocked()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            ownMenuState: 0, onHandBigMoney: 10, tradeOfferBigMoney: 0, amount: 5);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ResolveToTradeOffer_OwnSideLocked_IsTradeLocked()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            ownMenuState: 1, onHandBigMoney: 10, tradeOfferBigMoney: 0, amount: 5);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.TradeLocked, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveToTradeOffer_BothSidesConfirmed_IsTradeLocked()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            ownMenuState: 2, onHandBigMoney: 10, tradeOfferBigMoney: 0, amount: 5);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.TradeLocked, result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_TradeLockedTakesPrecedenceOverQuantityCheck()
    {
        // Even a would-otherwise-be-invalid amount is reported as TradeLocked first, matching the legacy's
        // trade-lock-guard-before-anything-else ordering.
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            ownMenuState: 1, onHandBigMoney: 10, tradeOfferBigMoney: 0, amount: 0);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.TradeLocked, result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_AmountBelowOne_IsQuantityOutOfRange()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            ownMenuState: 0, onHandBigMoney: 10, tradeOfferBigMoney: 0, amount: 0);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.QuantityOutOfRange, result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_AmountExceedsOnHandBalance_IsInsufficientSourceBalance()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            ownMenuState: 0, onHandBigMoney: 5, tradeOfferBigMoney: 0, amount: 6);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.InsufficientSourceBalance,
            result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_ResultingTradeOfferExceedsCap_IsDestinationOverflow()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            ownMenuState: 0, onHandBigMoney: TradeBigMoneyPlacementResolver.BigMoneyCap,
            tradeOfferBigMoney: TradeBigMoneyPlacementResolver.BigMoneyCap, amount: 1);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_ResultingTradeOfferExactlyAtCap_Succeeds()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            ownMenuState: 0, onHandBigMoney: TradeBigMoneyPlacementResolver.BigMoneyCap, tradeOfferBigMoney: 0,
            amount: TradeBigMoneyPlacementResolver.BigMoneyCap);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NewOnHandBigMoney);
        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyCap, result.NewTradeOfferBigMoney);
    }

    [Fact]
    public void ResolveToTradeOffer_Success_MovesAmountBetweenBothBalances()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            ownMenuState: 0, onHandBigMoney: 20, tradeOfferBigMoney: 3, amount: 10);

        Assert.True(result.Succeeded);
        Assert.Equal(10, result.NewOnHandBigMoney);
        Assert.Equal(13, result.NewTradeOfferBigMoney);
    }

    // ---- tSort 243 -- ResolveFromTradeOffer ----

    [Fact]
    public void ResolveFromTradeOffer_OwnSideLocked_IsTradeLocked()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            ownMenuState: 1, tradeOfferBigMoney: 10, onHandBigMoney: 0, amount: 5);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.TradeLocked, result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_AmountBelowOne_IsQuantityOutOfRange()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            ownMenuState: 0, tradeOfferBigMoney: 10, onHandBigMoney: 0, amount: -1);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.QuantityOutOfRange, result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_AmountExceedsTradeOfferBalance_IsInsufficientSourceBalance()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            ownMenuState: 0, tradeOfferBigMoney: 5, onHandBigMoney: 0, amount: 6);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.InsufficientSourceBalance,
            result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_ResultingOnHandExceedsCap_IsDestinationOverflow()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            ownMenuState: 0, tradeOfferBigMoney: TradeBigMoneyPlacementResolver.BigMoneyCap,
            onHandBigMoney: TradeBigMoneyPlacementResolver.BigMoneyCap, amount: 1);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_Success_MovesAmountBetweenBothBalances()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            ownMenuState: 0, tradeOfferBigMoney: 15, onHandBigMoney: 5, amount: 10);

        Assert.True(result.Succeeded);
        Assert.Equal(15, result.NewOnHandBigMoney);
        Assert.Equal(5, result.NewTradeOfferBigMoney);
    }

    [Fact]
    public void ResolveFromTradeOffer_NoTradeLockGuard_WhenNeitherSideHasConfirmed()
    {
        // The guard only fires at MenuState >= 1 -- an open-but-unconfirmed trade window (state 0) never blocks.
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            ownMenuState: 0, tradeOfferBigMoney: 15, onHandBigMoney: 5, amount: 10);

        Assert.NotEqual(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.TradeLocked, result.Outcome);
    }
}

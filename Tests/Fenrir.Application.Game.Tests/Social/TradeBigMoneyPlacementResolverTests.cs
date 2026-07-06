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
            0, 10, 0, 5);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ResolveToTradeOffer_OwnSideLocked_IsTradeLocked()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            1, 10, 0, 5);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.TradeLocked, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveToTradeOffer_BothSidesConfirmed_IsTradeLocked()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            2, 10, 0, 5);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.TradeLocked, result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_TradeLockedTakesPrecedenceOverQuantityCheck()
    {
        // Even a would-otherwise-be-invalid amount is reported as TradeLocked first, matching the legacy's
        // trade-lock-guard-before-anything-else ordering.
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            1, 10, 0, 0);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.TradeLocked, result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_AmountBelowOne_IsQuantityOutOfRange()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            0, 10, 0, 0);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.QuantityOutOfRange, result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_AmountExceedsOnHandBalance_IsInsufficientSourceBalance()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            0, 5, 0, 6);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.InsufficientSourceBalance,
            result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_ResultingTradeOfferExceedsCap_IsDestinationOverflow()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            0, TradeBigMoneyPlacementResolver.BigMoneyCap,
            TradeBigMoneyPlacementResolver.BigMoneyCap, 1);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveToTradeOffer_ResultingTradeOfferExactlyAtCap_Succeeds()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            0, TradeBigMoneyPlacementResolver.BigMoneyCap, 0,
            TradeBigMoneyPlacementResolver.BigMoneyCap);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NewOnHandBigMoney);
        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyCap, result.NewTradeOfferBigMoney);
    }

    [Fact]
    public void ResolveToTradeOffer_Success_MovesAmountBetweenBothBalances()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveToTradeOffer(
            0, 20, 3, 10);

        Assert.True(result.Succeeded);
        Assert.Equal(10, result.NewOnHandBigMoney);
        Assert.Equal(13, result.NewTradeOfferBigMoney);
    }

    // ---- tSort 243 -- ResolveFromTradeOffer ----

    [Fact]
    public void ResolveFromTradeOffer_OwnSideLocked_IsTradeLocked()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            1, 10, 0, 5);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.TradeLocked, result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_AmountBelowOne_IsQuantityOutOfRange()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            0, 10, 0, -1);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.QuantityOutOfRange, result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_AmountExceedsTradeOfferBalance_IsInsufficientSourceBalance()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            0, 5, 0, 6);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.InsufficientSourceBalance,
            result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_ResultingOnHandExceedsCap_IsDestinationOverflow()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            0, TradeBigMoneyPlacementResolver.BigMoneyCap,
            TradeBigMoneyPlacementResolver.BigMoneyCap, 1);

        Assert.Equal(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveFromTradeOffer_Success_MovesAmountBetweenBothBalances()
    {
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            0, 15, 5, 10);

        Assert.True(result.Succeeded);
        Assert.Equal(15, result.NewOnHandBigMoney);
        Assert.Equal(5, result.NewTradeOfferBigMoney);
    }

    [Fact]
    public void ResolveFromTradeOffer_NoTradeLockGuard_WhenNeitherSideHasConfirmed()
    {
        // The guard only fires at MenuState >= 1 -- an open-but-unconfirmed trade window (state 0) never blocks.
        var result = TradeBigMoneyPlacementResolver.ResolveFromTradeOffer(
            0, 15, 5, 10);

        Assert.NotEqual(TradeBigMoneyPlacementResolver.BigMoneyPlacementOutcome.TradeLocked, result.Outcome);
    }
}

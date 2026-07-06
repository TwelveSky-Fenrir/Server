using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

/// <summary>
///     Coverage for <see cref="BigMoneyUnitConversionPolicy" />, the pure policy behind tSort 246
///     (money -&gt; BigMoney) and 247 (BigMoney -&gt; money). Does not depend on any dispatch wiring.
/// </summary>
public class BigMoneyUnitConversionPolicyTests
{
    // ---- tSort 246 -- ResolveMoneyToBigMoney ----

    [Fact]
    public void ResolveMoneyToBigMoney_QuantityBelowOneBillion_IsQuantityBelowMinimum()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            requestedQuantity: 999_999_999, inventoryMoney: 2_000_000_000, inventoryBigMoney: 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.QuantityBelowMinimum, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_ZeroQuantity_IsQuantityBelowMinimum()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            requestedQuantity: 0, inventoryMoney: 2_000_000_000, inventoryBigMoney: 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.QuantityBelowMinimum, result.Outcome);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_NegativeQuantity_IsQuantityBelowMinimum()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            requestedQuantity: -1, inventoryMoney: 2_000_000_000, inventoryBigMoney: 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.QuantityBelowMinimum, result.Outcome);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_MoneyBelowRequestedQuantity_IsInsufficientSourceBalance()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            requestedQuantity: 1_000_000_000, inventoryMoney: 999_999_999, inventoryBigMoney: 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.InsufficientSourceBalance,
            result.Outcome);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_DestinationAtCap_IsDestinationOverflow()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            requestedQuantity: 1_000_000_000, inventoryMoney: 1_000_000_000,
            inventoryBigMoney: BigMoneyUnitConversionPolicy.BigMoneyCap);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_DestinationOneBelowCap_Succeeds()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            requestedQuantity: 1_000_000_000, inventoryMoney: 1_000_000_000,
            inventoryBigMoney: BigMoneyUnitConversionPolicy.BigMoneyCap - 1);

        Assert.True(result.Succeeded);
        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyCap, result.NewInventoryBigMoney);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_ExactlyOneBillion_IsAFair1To1Exchange()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            requestedQuantity: 1_000_000_000, inventoryMoney: 1_000_000_000, inventoryBigMoney: 5);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NewInventoryMoney);
        Assert.Equal(6, result.NewInventoryBigMoney);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_QuantityAboveOneBillion_DebitsFullQuantityButGrantsOnlyOneUnit()
    {
        // A client that overpays loses the excess: no partial refund, no extra units granted.
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            requestedQuantity: 1_500_000_000, inventoryMoney: 2_000_000_000, inventoryBigMoney: 5);

        Assert.True(result.Succeeded);
        Assert.Equal(500_000_000, result.NewInventoryMoney);
        Assert.Equal(6, result.NewInventoryBigMoney);
    }

    // ---- tSort 247 -- ResolveBigMoneyToMoney ----

    [Fact]
    public void ResolveBigMoneyToMoney_ZeroQuantity_IsQuantityBelowMinimum()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            requestedQuantity: 0, inventoryBigMoney: 10, inventoryMoney: 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.QuantityBelowMinimum, result.Outcome);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_NegativeQuantity_IsQuantityBelowMinimum()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            requestedQuantity: -5, inventoryBigMoney: 10, inventoryMoney: 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.QuantityBelowMinimum, result.Outcome);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_BigMoneyBelowRequestedQuantity_IsInsufficientSourceBalance()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            requestedQuantity: 5, inventoryBigMoney: 4, inventoryMoney: 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.InsufficientSourceBalance,
            result.Outcome);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_DestinationWouldExceedCeiling_IsDestinationOverflow()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            requestedQuantity: 1, inventoryBigMoney: 1,
            inventoryMoney: BigMoneyUnitConversionPolicy.MoneyCeiling - 999_999_999);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_DestinationExactlyAtCeiling_Succeeds()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            requestedQuantity: 1, inventoryBigMoney: 1,
            inventoryMoney: BigMoneyUnitConversionPolicy.MoneyCeiling - 1_000_000_000);

        Assert.True(result.Succeeded);
        Assert.Equal(BigMoneyUnitConversionPolicy.MoneyCeiling, result.NewInventoryMoney);
        Assert.Equal(0, result.NewInventoryBigMoney);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_Success_MovesExactlyOneUnitForExactlyOneBillion()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            requestedQuantity: 1, inventoryBigMoney: 5, inventoryMoney: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1_000_000_000, result.NewInventoryMoney);
        Assert.Equal(4, result.NewInventoryBigMoney);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_QuantityAboveOne_OnlyRaisesEligibilityBar_MutationStaysFixed()
    {
        // Over-supplying the quantity only requires a correspondingly larger BigMoney balance; it has no
        // additional effect on the amounts actually moved.
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            requestedQuantity: 5, inventoryBigMoney: 5, inventoryMoney: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1_000_000_000, result.NewInventoryMoney);
        Assert.Equal(4, result.NewInventoryBigMoney);
    }
}

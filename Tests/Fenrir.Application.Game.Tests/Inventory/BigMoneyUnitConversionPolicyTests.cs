using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

public class BigMoneyUnitConversionPolicyTests
{
    [Fact]
    public void ResolveMoneyToBigMoney_QuantityBelowOneBillion_IsQuantityBelowMinimum()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            999_999_999, 2_000_000_000, 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.QuantityBelowMinimum, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_ZeroQuantity_IsQuantityBelowMinimum()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            0, 2_000_000_000, 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.QuantityBelowMinimum, result.Outcome);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_NegativeQuantity_IsQuantityBelowMinimum()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            -1, 2_000_000_000, 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.QuantityBelowMinimum, result.Outcome);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_MoneyBelowRequestedQuantity_IsInsufficientSourceBalance()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            1_000_000_000, 999_999_999, 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.InsufficientSourceBalance,
            result.Outcome);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_DestinationAtCap_IsDestinationOverflow()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            1_000_000_000, 1_000_000_000,
            BigMoneyUnitConversionPolicy.BigMoneyCap);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_DestinationOneBelowCap_Succeeds()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            1_000_000_000, 1_000_000_000,
            BigMoneyUnitConversionPolicy.BigMoneyCap - 1);

        Assert.True(result.Succeeded);
        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyCap, result.NewInventoryBigMoney);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_ExactlyOneBillion_IsAFair1To1Exchange()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            1_000_000_000, 1_000_000_000, 5);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NewInventoryMoney);
        Assert.Equal(6, result.NewInventoryBigMoney);
    }

    [Fact]
    public void ResolveMoneyToBigMoney_QuantityAboveOneBillion_DebitsFullQuantityButGrantsOnlyOneUnit()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney(
            1_500_000_000, 2_000_000_000, 5);

        Assert.True(result.Succeeded);
        Assert.Equal(500_000_000, result.NewInventoryMoney);
        Assert.Equal(6, result.NewInventoryBigMoney);
    }


    [Fact]
    public void ResolveBigMoneyToMoney_ZeroQuantity_IsQuantityBelowMinimum()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            0, 10, 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.QuantityBelowMinimum, result.Outcome);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_NegativeQuantity_IsQuantityBelowMinimum()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            -5, 10, 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.QuantityBelowMinimum, result.Outcome);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_BigMoneyBelowRequestedQuantity_IsInsufficientSourceBalance()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            5, 4, 0);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.InsufficientSourceBalance,
            result.Outcome);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_DestinationWouldExceedCeiling_IsDestinationOverflow()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            1, 1,
            BigMoneyUnitConversionPolicy.MoneyCeiling - 999_999_999);

        Assert.Equal(BigMoneyUnitConversionPolicy.BigMoneyUnitConversionOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_DestinationExactlyAtCeiling_Succeeds()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            1, 1,
            BigMoneyUnitConversionPolicy.MoneyCeiling - 1_000_000_000);

        Assert.True(result.Succeeded);
        Assert.Equal(BigMoneyUnitConversionPolicy.MoneyCeiling, result.NewInventoryMoney);
        Assert.Equal(0, result.NewInventoryBigMoney);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_Success_MovesExactlyOneUnitForExactlyOneBillion()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            1, 5, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1_000_000_000, result.NewInventoryMoney);
        Assert.Equal(4, result.NewInventoryBigMoney);
    }

    [Fact]
    public void ResolveBigMoneyToMoney_QuantityAboveOne_OnlyRaisesEligibilityBar_MutationStaysFixed()
    {
        var result = BigMoneyUnitConversionPolicy.ResolveBigMoneyToMoney(
            5, 5, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1_000_000_000, result.NewInventoryMoney);
        Assert.Equal(4, result.NewInventoryBigMoney);
    }
}

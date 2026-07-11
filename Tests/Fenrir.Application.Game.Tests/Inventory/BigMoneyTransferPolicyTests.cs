using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

public class BigMoneyTransferPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolveInventoryToStore_QuantityBelowOne_IsQuantityBelowMinimum(long requestedQuantity)
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToStore(requestedQuantity, 10,
            0);

        Assert.Equal(BigMoneyTransferPolicy.TransferOutcome.QuantityBelowMinimum, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveInventoryToStore_AmountExceedsSource_IsInsufficientSourceBalance()
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToStore(11, 10,
            0);

        Assert.Equal(BigMoneyTransferPolicy.TransferOutcome.InsufficientSourceBalance, result.Outcome);
    }

    [Fact]
    public void ResolveInventoryToStore_AmountExactlyEqualsSource_Succeeds()
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToStore(10, 10,
            0);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NewSourceBigMoney);
        Assert.Equal(10, result.NewDestinationBigMoney);
    }

    [Fact]
    public void ResolveInventoryToStore_DestinationWouldExceedCap_IsDestinationOverflow()
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToStore(1, 999,
            BigMoneyTransferPolicy.BigMoneyCap);

        Assert.Equal(BigMoneyTransferPolicy.TransferOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveInventoryToStore_DestinationExactlyAtCap_Succeeds()
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToStore(1, 999,
            BigMoneyTransferPolicy.BigMoneyCap - 1);

        Assert.True(result.Succeeded);
        Assert.Equal(BigMoneyTransferPolicy.BigMoneyCap, result.NewDestinationBigMoney);
    }

    [Fact]
    public void ResolveStoreToInventory_Success_MovesAmountBothWays()
    {
        var result = BigMoneyTransferPolicy.ResolveStoreToInventory(100, 500,
            50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceBigMoney);
        Assert.Equal(150, result.NewDestinationBigMoney);
    }

    [Fact]
    public void ResolveInventoryToBank_Success_MovesAmountBothWays()
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToBank(100, 500,
            50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceBigMoney);
        Assert.Equal(150, result.NewDestinationBigMoney);
    }

    [Fact]
    public void ResolveBankToInventory_Success_MovesAmountBothWays()
    {
        var result = BigMoneyTransferPolicy.ResolveBankToInventory(100, 500,
            50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceBigMoney);
        Assert.Equal(150, result.NewDestinationBigMoney);
    }

    [Fact]
    public void ResolveBankToInventory_AmountExceedsSource_IsInsufficientSourceBalance()
    {
        var result = BigMoneyTransferPolicy.ResolveBankToInventory(501, 500,
            0);

        Assert.Equal(BigMoneyTransferPolicy.TransferOutcome.InsufficientSourceBalance, result.Outcome);
    }
}

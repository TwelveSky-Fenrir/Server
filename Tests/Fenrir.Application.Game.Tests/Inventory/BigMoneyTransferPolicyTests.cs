using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

/// <summary>
///     Coverage for <see cref="BigMoneyTransferPolicy" />, the pure policy behind tSort 241/244
///     (Inventory &lt;-&gt; Store BigMoney) and 242/245 (Inventory &lt;-&gt; Bank BigMoney). Does not depend on
///     any dispatch wiring.
/// </summary>
public class BigMoneyTransferPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolveInventoryToStore_QuantityBelowOne_IsQuantityBelowMinimum(long requestedQuantity)
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToStore(requestedQuantity, inventoryBigMoney: 10,
            storeBigMoney: 0);

        Assert.Equal(BigMoneyTransferPolicy.TransferOutcome.QuantityBelowMinimum, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveInventoryToStore_AmountExceedsSource_IsInsufficientSourceBalance()
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToStore(requestedQuantity: 11, inventoryBigMoney: 10,
            storeBigMoney: 0);

        Assert.Equal(BigMoneyTransferPolicy.TransferOutcome.InsufficientSourceBalance, result.Outcome);
    }

    [Fact]
    public void ResolveInventoryToStore_AmountExactlyEqualsSource_Succeeds()
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToStore(requestedQuantity: 10, inventoryBigMoney: 10,
            storeBigMoney: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NewSourceBigMoney);
        Assert.Equal(10, result.NewDestinationBigMoney);
    }

    [Fact]
    public void ResolveInventoryToStore_DestinationWouldExceedCap_IsDestinationOverflow()
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToStore(requestedQuantity: 1, inventoryBigMoney: 999,
            storeBigMoney: BigMoneyTransferPolicy.BigMoneyCap);

        Assert.Equal(BigMoneyTransferPolicy.TransferOutcome.DestinationOverflow, result.Outcome);
    }

    [Fact]
    public void ResolveInventoryToStore_DestinationExactlyAtCap_Succeeds()
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToStore(requestedQuantity: 1, inventoryBigMoney: 999,
            storeBigMoney: BigMoneyTransferPolicy.BigMoneyCap - 1);

        Assert.True(result.Succeeded);
        Assert.Equal(BigMoneyTransferPolicy.BigMoneyCap, result.NewDestinationBigMoney);
    }

    [Fact]
    public void ResolveStoreToInventory_Success_MovesAmountBothWays()
    {
        var result = BigMoneyTransferPolicy.ResolveStoreToInventory(requestedQuantity: 100, storeBigMoney: 500,
            inventoryBigMoney: 50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceBigMoney);
        Assert.Equal(150, result.NewDestinationBigMoney);
    }

    [Fact]
    public void ResolveInventoryToBank_Success_MovesAmountBothWays()
    {
        var result = BigMoneyTransferPolicy.ResolveInventoryToBank(requestedQuantity: 100, inventoryBigMoney: 500,
            bankBigMoney: 50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceBigMoney);
        Assert.Equal(150, result.NewDestinationBigMoney);
    }

    [Fact]
    public void ResolveBankToInventory_Success_MovesAmountBothWays()
    {
        var result = BigMoneyTransferPolicy.ResolveBankToInventory(requestedQuantity: 100, bankBigMoney: 500,
            inventoryBigMoney: 50);

        Assert.True(result.Succeeded);
        Assert.Equal(400, result.NewSourceBigMoney);
        Assert.Equal(150, result.NewDestinationBigMoney);
    }

    [Fact]
    public void ResolveBankToInventory_AmountExceedsSource_IsInsufficientSourceBalance()
    {
        var result = BigMoneyTransferPolicy.ResolveBankToInventory(requestedQuantity: 501, bankBigMoney: 500,
            inventoryBigMoney: 0);

        Assert.Equal(BigMoneyTransferPolicy.TransferOutcome.InsufficientSourceBalance, result.Outcome);
    }
}

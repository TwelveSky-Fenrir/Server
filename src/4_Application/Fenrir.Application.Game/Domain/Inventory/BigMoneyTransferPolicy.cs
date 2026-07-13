namespace Fenrir.Application.Game.Domain.Inventory;

public static class BigMoneyTransferPolicy
{
    public enum TransferOutcome
    {
        Success,

        QuantityBelowMinimum,

        InsufficientSourceBalance,

        DestinationOverflow
    }

    public const long BigMoneyCap = 999;

    public static TransferResult ResolveInventoryToStore(
        long requestedQuantity, long inventoryBigMoney, long storeBigMoney)
    {
        return Resolve(requestedQuantity, inventoryBigMoney, storeBigMoney);
    }

    public static TransferResult ResolveStoreToInventory(
        long requestedQuantity, long storeBigMoney, long inventoryBigMoney)
    {
        return Resolve(requestedQuantity, storeBigMoney, inventoryBigMoney);
    }

    public static TransferResult ResolveInventoryToBank(
        long requestedQuantity, long inventoryBigMoney, long bankBigMoney)
    {
        return Resolve(requestedQuantity, inventoryBigMoney, bankBigMoney);
    }

    public static TransferResult ResolveBankToInventory(
        long requestedQuantity, long bankBigMoney, long inventoryBigMoney)
    {
        return Resolve(requestedQuantity, bankBigMoney, inventoryBigMoney);
    }

    private static TransferResult Resolve(long requestedQuantity, long sourceBigMoney, long destinationBigMoney)
    {
        if (requestedQuantity < 1)
            return new TransferResult(TransferOutcome.QuantityBelowMinimum, sourceBigMoney, destinationBigMoney);

        if (requestedQuantity > sourceBigMoney)
            return new TransferResult(TransferOutcome.InsufficientSourceBalance, sourceBigMoney, destinationBigMoney);

        var projectedDestination = destinationBigMoney + requestedQuantity;
        if (projectedDestination > BigMoneyCap)
            return new TransferResult(TransferOutcome.DestinationOverflow, sourceBigMoney, destinationBigMoney);

        return new TransferResult(TransferOutcome.Success, sourceBigMoney - requestedQuantity, projectedDestination);
    }

    public readonly record struct TransferResult(
        TransferOutcome Outcome,
        long NewSourceBigMoney,
        long NewDestinationBigMoney)
    {
        public bool Succeeded => Outcome == TransferOutcome.Success;
    }
}

namespace Fenrir.Application.Game.Domain.Inventory;

public static class SaveBankMoneyPolicy
{
    public enum TransferOutcome
    {
        Success,

        InvalidQuantity,

        InsufficientSource,

        DestinationOverflow
    }

    public const long MaxMoney = 2_000_000_000;

    public static TransferResult ResolveDeposit(long requestedAmount, long inventoryMoney, long bankMoney)
    {
        return Resolve(requestedAmount, inventoryMoney, bankMoney);
    }

    public static TransferResult ResolveWithdraw(long requestedAmount, long bankMoney, long inventoryMoney)
    {
        return Resolve(requestedAmount, bankMoney, inventoryMoney);
    }

    private static TransferResult Resolve(long requestedAmount, long sourceMoney, long destinationMoney)
    {
        if (requestedAmount <= 0)
            return new TransferResult(TransferOutcome.InvalidQuantity, sourceMoney, destinationMoney);

        if (requestedAmount > sourceMoney)
            return new TransferResult(TransferOutcome.InsufficientSource, sourceMoney, destinationMoney);

        var projectedDestination = destinationMoney + requestedAmount;
        if (projectedDestination > MaxMoney)
            return new TransferResult(TransferOutcome.DestinationOverflow, sourceMoney, destinationMoney);

        return new TransferResult(TransferOutcome.Success, sourceMoney - requestedAmount, projectedDestination);
    }

    public readonly record struct TransferResult(
        TransferOutcome Outcome,
        long NewSourceMoney,
        long NewDestinationMoney)
    {
        public bool Succeeded => Outcome == TransferOutcome.Success;
    }
}

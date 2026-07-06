namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Pure, Zone-independent policy for the Save/Bank money-transfer family (<c>CZ_PROCESS_DATA_SEND</c> tSort 231
///     deposit, 232 withdraw) between the inventory-money counter (<c>wMoney</c>) and the bank-money counter
///     (<c>wUSaveMoney</c>) -- two independent scalar counters, neither of which is modeled on
///     <c>PlayerRuntimeState</c> today. Deliberately unopinionated about where those counters live; the caller
///     supplies both current values and receives both new values back.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3275-3342 (<c>ProcessForInventoryMoneyToSaveMoney</c>,
///     <c>ProcessForSaveMoneyToInventoryMoney</c>) ; Server/Header/function.h:132-140 (<c>CheckOverMaximum</c>,
///     the 64-bit-widened overflow guard reused verbatim here by taking/returning <see langword="long" />) ;
///     Server/Header/Protocol/DEFINE.h:365 (<c>MAX_NUMBER_SIZE</c> = 2,000,000,000) ; DEFINE.h:741/:724
///     (<c>wUSaveMoney</c>/<c>wMoney</c> field aliases).
/// </remarks>
public static class SaveBankMoneyPolicy
{
    public enum TransferOutcome
    {
        Success,

        /// <summary>Requested amount is not a positive whole number.</summary>
        InvalidQuantity,

        /// <summary>Requested amount exceeds the source counter's current value.</summary>
        InsufficientSource,

        /// <summary>Requested amount would push the destination counter's value past <see cref="MaxMoney" />.</summary>
        DestinationOverflow
    }

    /// <summary>MAX_NUMBER_SIZE (DEFINE.h:365) -- the shared cap both counters are checked against.</summary>
    public const long MaxMoney = 2_000_000_000;

    /// <summary>tSort 231 -- inventory money to bank money.</summary>
    public static TransferResult ResolveDeposit(long requestedAmount, long inventoryMoney, long bankMoney)
    {
        return Resolve(requestedAmount, inventoryMoney, bankMoney);
    }

    /// <summary>tSort 232 -- bank money to inventory money.</summary>
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

        // Both operands are already long (CheckOverMaximum's own 64-bit widening), so this addition cannot itself
        // overflow regardless of MaxMoney's 32-bit-friendly magnitude.
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

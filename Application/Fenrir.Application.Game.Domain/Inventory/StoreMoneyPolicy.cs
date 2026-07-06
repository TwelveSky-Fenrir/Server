namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Pure, Zone-independent policy for the Store/coffre money-transfer family (<c>CZ_PROCESS_DATA_SEND</c> tSort
///     226 deposit, 227 withdraw) between the wallet-money counter (<c>wMoney</c>) and the store-money counter
///     (<c>wStoreMoney</c>) -- two independent scalar counters, neither modeled on <c>PlayerRuntimeState</c> today
///     (see <see cref="StoreItemTransferPolicy" />'s own remarks for the identical gap on the item side).
///     Deliberately unopinionated about where those counters live; the caller supplies both current values and
///     receives both new values back.
///     <para>
///         Store's own separate high-denomination ("1B"/billion-unit) money pool (<c>BigMoney</c>/
///         <c>BigStoreMoney</c>) has its own transfer sub-actions elsewhere in the same dispatch and is explicitly
///         out of scope here -- do not reuse this policy for it without a fresh behavior contract.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:2903-2935 (<c>ProcessForInventoryMoneyToStoreMoney</c>) ;
///     :2937-2969 (<c>ProcessForStoreMoneyToInventoryMoney</c>) ; Server/Header/function.h:132-140
///     (<c>CheckOverMaximum</c>, the 64-bit-widened overflow guard reused verbatim here by taking/returning
///     <see langword="long" />) ; Server/Header/Protocol/DEFINE.h:365-367 (<c>MAX_NUMBER_SIZE</c> = 2,000,000,000).
///     <para>
///         Every failure here is a HARD failure in the caller's dispatch (disconnect, no partial mutation, no
///         acknowledgement of any kind) -- unlike every <see cref="StoreItemTransferPolicy" /> outcome, which is a
///         soft failure (clean failure response, no disconnect). This policy itself is agnostic to that
///         distinction; translating a non-<see cref="TransferOutcome.Success" /> result into a disconnect is the
///         caller's job, not this type's.
///     </para>
/// </remarks>
public static class StoreMoneyPolicy
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

    /// <summary>tSort 226 -- wallet money to store money.</summary>
    public static TransferResult ResolveDeposit(long requestedAmount, long walletMoney, long storeMoney)
    {
        return Resolve(requestedAmount, walletMoney, storeMoney);
    }

    /// <summary>tSort 227 -- store money to wallet money.</summary>
    public static TransferResult ResolveWithdraw(long requestedAmount, long storeMoney, long walletMoney)
    {
        return Resolve(requestedAmount, storeMoney, walletMoney);
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

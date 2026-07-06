namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Pure, Zone-independent policy for the two non-trade "1B"/"1 Md" BigMoney-to-BigMoney transfer pairs:
///     Inventory &lt;-&gt; Store (<c>CZ_PROCESS_DATA_SEND</c> tSort 241/244) and Inventory &lt;-&gt; Bank/save
///     (tSort 242/245). Both pairs move the literal, symmetric <c>requestedQuantity</c> off the source and onto
///     the destination -- no fixed-unit substitution applies here, unlike
///     <see cref="BigMoneyUnitConversionPolicy" />'s tSort 246/247. Deliberately unopinionated about where the
///     four underlying counters (<c>aBigMoney</c>, <c>aBigStoreMoney</c>, <c>uBigSaveMoney</c>) live; the caller
///     supplies both current values and receives both new values back.
///     <para>
///         The Inventory &lt;-&gt; Trade BigMoney pair (tSort 240/243) is intentionally NOT covered by this type:
///         it additionally carries a trade-lock guard that these two pairs never had, so it lives in its own
///         resolver instead --
///         <see cref="Fenrir.Application.Game.Domain.Social.Trade.TradeBigMoneyPlacementResolver" />.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3666-3699 (<c>ProcessForInventoryMoneyTo1BStoreMoney</c>,
///     tSort 241, no trade-lock guard) ; :3701-3734 (<c>ProcessFor1BStoreMoneyToInventoryMoney</c>, tSort 244) ;
///     :3736-3769 (<c>ProcessForInventoryMoneyTo1BSaveMoney</c>, tSort 242) ; :3771-3804
///     (<c>ProcessFor1BSaveMoneyToInventoryMoney</c>, tSort 245) ; Server/Header/function.h:132-150
///     (<c>CheckOverBigMoneyMaximum</c> -- plain 32-bit sum, unlike <c>CheckOverMaximum</c>'s 64-bit widening;
///     reproduced here with <see langword="long" /> operands purely for a wider C# call surface, since every
///     call site reachable through these four tSorts already bounds the addend by a prior "source has at least
///     this much" check against a counter itself capped at <see cref="BigMoneyCap" />, so the width difference
///     is not reachable through this family -- a standing legacy inconsistency preserved as fact, not "fixed"
///     here) ; Server/Header/Protocol/DEFINE.h:365-367 (<c>MAX_NUMBER_SIZE2</c> = 999, the BigMoney cap).
///     <para>
///         Every failure here is a HARD failure in the caller's dispatch (disconnect, no partial mutation, no
///         acknowledgement of any kind, per Server/ts25zone/S04_MyWork04.cpp:726-773's return-immediately-on-
///         failure pattern) -- this policy itself is agnostic to that; translating a non-
///         <see cref="TransferOutcome.Success" /> result into a disconnect is the caller's job.
///     </para>
/// </remarks>
public static class BigMoneyTransferPolicy
{
    public enum TransferOutcome
    {
        Success,

        /// <summary>Requested amount is less than 1.</summary>
        QuantityBelowMinimum,

        /// <summary>The moving side's balance does not already hold at least the requested amount.</summary>
        InsufficientSourceBalance,

        /// <summary>The receiving side's balance, after adding the requested amount, would exceed <see cref="BigMoneyCap" />.</summary>
        DestinationOverflow
    }

    /// <summary>MAX_NUMBER_SIZE2 (DEFINE.h:367) -- the shared cap every BigMoney-family counter is checked against.</summary>
    public const long BigMoneyCap = 999;

    /// <summary>tSort 241 -- Inventory BigMoney to Store BigMoney.</summary>
    public static TransferResult ResolveInventoryToStore(
        long requestedQuantity, long inventoryBigMoney, long storeBigMoney)
    {
        return Resolve(requestedQuantity, inventoryBigMoney, storeBigMoney);
    }

    /// <summary>tSort 244 -- Store BigMoney to Inventory BigMoney.</summary>
    public static TransferResult ResolveStoreToInventory(
        long requestedQuantity, long storeBigMoney, long inventoryBigMoney)
    {
        return Resolve(requestedQuantity, storeBigMoney, inventoryBigMoney);
    }

    /// <summary>tSort 242 -- Inventory BigMoney to Bank/save BigMoney.</summary>
    public static TransferResult ResolveInventoryToBank(
        long requestedQuantity, long inventoryBigMoney, long bankBigMoney)
    {
        return Resolve(requestedQuantity, inventoryBigMoney, bankBigMoney);
    }

    /// <summary>tSort 245 -- Bank/save BigMoney to Inventory BigMoney.</summary>
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

    /// <summary>NewSourceBigMoney/NewDestinationBigMoney are both sides' post-transfer balances, valid only when Succeeded.</summary>
    public readonly record struct TransferResult(
        TransferOutcome Outcome,
        long NewSourceBigMoney,
        long NewDestinationBigMoney)
    {
        public bool Succeeded => Outcome == TransferOutcome.Success;
    }
}

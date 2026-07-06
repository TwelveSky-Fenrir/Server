namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Pure, Zone-independent policy for the "1B"/"1 Md" unit-conversion family (<c>CZ_PROCESS_DATA_SEND</c>
///     tSort 246 money-&gt;BigMoney, tSort 247 BigMoney-&gt;money) between the ordinary wallet-money counter
///     (<c>wMoney</c>/<c>aMoney</c>) and the whole-billion-unit BigMoney counter (<c>wBigMoney</c>/
///     <c>aBigMoney</c>). Deliberately unopinionated about where those counters live; the caller supplies both
///     current values and receives both new values back.
///     <para>
///         Unlike every other counter pair in this family (see <see cref="BigMoneyTransferPolicy" /> and
///         <c>Fenrir.Application.Game.Domain.Social.Trade.TradeBigMoneyPlacementResolver</c>), the amount moved
///         is NOT the requested quantity on both sides: the requested quantity only ever gates eligibility, and
///         the actual mutation is always exactly one <see cref="BigMoneyUnitValue" /> of ordinary money against
///         exactly one whole BigMoney unit. A caller that requests more than <see cref="BigMoneyUnitValue" /> on
///         tSort 246 pays the excess with nothing in return -- that is legacy behavior, not a bug, and must not
///         be "fixed" here.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3512-3545 (<c>ProcessForInventoryMoneyTo1BInventoryMoney</c>,
///     tSort 246: quantity floor of one billion, source-balance check, destination-cap check, fixed
///     +1/-full-quantity mutation) ; :3547-3580 (<c>ProcessFor1BInventoryMoneyToInventoryMoney</c>, tSort 247:
///     quantity floor of one, source-balance check, <c>CheckOverMaximum</c> destination-cap check, fixed
///     -1/+one-billion mutation) ; Server/Header/function.h:132-150 (<c>CheckOverMaximum</c> vs.
///     <c>CheckOverBigMoneyMaximum</c>) ; Server/Header/Protocol/DEFINE.h:365-367 (<c>MAX_NUMBER_SIZE</c> =
///     2,000,000,000 ; <c>MAX_NUMBER_SIZE1</c> = 1,000,000,000, the fixed per-unit value ; <c>MAX_NUMBER_SIZE2</c>
///     = 999, the BigMoney counter cap).
///     <para>
///         Every failure here is a HARD failure in the caller's dispatch (disconnect, no partial mutation, no
///         acknowledgement of any kind, per Server/ts25zone/S04_MyWork04.cpp:726-773's return-immediately-on-
///         failure pattern) -- this policy itself is agnostic to that; translating a non-
///         <see cref="BigMoneyUnitConversionOutcome.Success" /> result into a disconnect is the caller's job.
///     </para>
/// </remarks>
public static class BigMoneyUnitConversionPolicy
{
    /// <summary>MAX_NUMBER_SIZE (DEFINE.h:365) -- the general money ceiling, tSort 247's destination cap.</summary>
    public const long MoneyCeiling = 2_000_000_000;

    /// <summary>
    ///     MAX_NUMBER_SIZE1 (DEFINE.h:366) -- both the mandatory eligibility floor for tSort 246's requested
    ///     quantity and the fixed amount of ordinary money one BigMoney unit is always worth.
    /// </summary>
    public const long BigMoneyUnitValue = 1_000_000_000;

    /// <summary>MAX_NUMBER_SIZE2 (DEFINE.h:367) -- the BigMoney counter's own cap, tSort 246's destination cap.</summary>
    public const long BigMoneyCap = 999;

    public enum BigMoneyUnitConversionOutcome
    {
        Success,

        /// <summary>Requested quantity is below the operation's required minimum (one billion for 246, one for 247).</summary>
        QuantityBelowMinimum,

        /// <summary>The moving side's balance does not already hold at least the requested amount.</summary>
        InsufficientSourceBalance,

        /// <summary>The receiving side's balance, after the fixed mutation, would exceed its own cap.</summary>
        DestinationOverflow
    }

    /// <summary>NewInventoryMoney/NewInventoryBigMoney are both counters' post-conversion values, valid only when Succeeded.</summary>
    public readonly record struct BigMoneyUnitConversionResult(
        BigMoneyUnitConversionOutcome Outcome,
        long NewInventoryMoney,
        long NewInventoryBigMoney)
    {
        public bool Succeeded => Outcome == BigMoneyUnitConversionOutcome.Success;
    }

    /// <summary>
    ///     tSort 246 -- ordinary money to BigMoney. The full <paramref name="requestedQuantity" /> is debited from
    ///     <paramref name="inventoryMoney" /> regardless of size, but <paramref name="inventoryBigMoney" /> only
    ///     ever increases by exactly one unit; only supplying exactly <see cref="BigMoneyUnitValue" /> is a fair
    ///     1:1 exchange.
    /// </summary>
    public static BigMoneyUnitConversionResult ResolveMoneyToBigMoney(
        long requestedQuantity, long inventoryMoney, long inventoryBigMoney)
    {
        if (requestedQuantity < BigMoneyUnitValue)
            return new BigMoneyUnitConversionResult(BigMoneyUnitConversionOutcome.QuantityBelowMinimum,
                inventoryMoney, inventoryBigMoney);

        if (requestedQuantity > inventoryMoney)
            return new BigMoneyUnitConversionResult(BigMoneyUnitConversionOutcome.InsufficientSourceBalance,
                inventoryMoney, inventoryBigMoney);

        var newInventoryBigMoney = inventoryBigMoney + 1;
        if (newInventoryBigMoney > BigMoneyCap)
            return new BigMoneyUnitConversionResult(BigMoneyUnitConversionOutcome.DestinationOverflow,
                inventoryMoney, inventoryBigMoney);

        return new BigMoneyUnitConversionResult(BigMoneyUnitConversionOutcome.Success,
            inventoryMoney - requestedQuantity, newInventoryBigMoney);
    }

    /// <summary>
    ///     tSort 247 -- BigMoney to ordinary money. <paramref name="inventoryBigMoney" /> always decreases by
    ///     exactly one unit and <paramref name="inventoryMoney" /> always increases by exactly
    ///     <see cref="BigMoneyUnitValue" />, regardless of how far above the required minimum of 1 the supplied
    ///     <paramref name="requestedQuantity" /> is -- over-supplying it only raises the eligibility bar, with no
    ///     benefit or penalty.
    /// </summary>
    public static BigMoneyUnitConversionResult ResolveBigMoneyToMoney(
        long requestedQuantity, long inventoryBigMoney, long inventoryMoney)
    {
        if (requestedQuantity < 1)
            return new BigMoneyUnitConversionResult(BigMoneyUnitConversionOutcome.QuantityBelowMinimum,
                inventoryMoney, inventoryBigMoney);

        if (requestedQuantity > inventoryBigMoney)
            return new BigMoneyUnitConversionResult(BigMoneyUnitConversionOutcome.InsufficientSourceBalance,
                inventoryMoney, inventoryBigMoney);

        var newInventoryMoney = inventoryMoney + BigMoneyUnitValue;
        if (newInventoryMoney > MoneyCeiling)
            return new BigMoneyUnitConversionResult(BigMoneyUnitConversionOutcome.DestinationOverflow,
                inventoryMoney, inventoryBigMoney);

        return new BigMoneyUnitConversionResult(BigMoneyUnitConversionOutcome.Success,
            newInventoryMoney, inventoryBigMoney - 1);
    }
}

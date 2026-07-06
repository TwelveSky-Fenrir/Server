namespace Fenrir.Application.Game.Domain.Social.Trade;

/// <summary>
///     Pure resolver for the trade-window "normal money" placement family: moving on-hand money into the
///     caller's trade offer (<see cref="ResolveToTradeOffer" />, tSort 221) and back out
///     (<see cref="ResolveFromTradeOffer" />, tSort 222). No I/O -- independently unit-testable.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:2458-2497 (<c>ProcessForInventoryMoneyToTradeMoney</c>,
///     tSort 221, full body), :2499-2538 (<c>ProcessForTradeMoneyToInventoryMoney</c>, tSort 222, full body) ;
///     Server/Header/Protocol/DEFINE.h:365 (<c>MAX_NUMBER_SIZE</c>) ; Server/Header/function.h:132-140
///     (<c>CheckOverMaximum</c>, the widened-arithmetic overflow guard both functions use).
///     <para>
///         Out of scope by contract: the separate "1,000,000,000-per-unit" big-money counter has its own
///         tSort family (240-247) and must never be touched by these two methods -- callers must route that
///         counter through <c>TradeOfferSide.BigMoney</c> via a different resolver, not this one.
///     </para>
///     <para>
///         Unlike the item family (tSort 218/219/220), there is no explicit upper-bound check on the
///         requested amount by itself -- only "at least 1" plus the implicit bounds of the source balance and
///         the destination ceiling below.
///     </para>
/// </remarks>
public static class TradeMoneyPlacementResolver
{
    public enum MoneyPlacementOutcome
    {
        Success,

        /// <summary>Requested amount is less than 1.</summary>
        QuantityOutOfRange,

        /// <summary>The moving side's balance does not already hold at least the requested amount.</summary>
        InsufficientSourceBalance,

        /// <summary>The receiving side's balance, after adding the requested amount, would exceed <see cref="MoneyCeiling" />.</summary>
        DestinationOverflow
    }

    /// <summary>MAX_NUMBER_SIZE, DEFINE.h:365.</summary>
    public const long MoneyCeiling = 2_000_000_000;

    /// <summary>tSort 221 -- ProcessForInventoryMoneyToTradeMoney: on-hand money moves into the trade offer.</summary>
    public static MoneyPlacementResult ResolveToTradeOffer(long onHandMoney, long tradeOfferMoney, long amount)
    {
        if (amount < 1)
            return new MoneyPlacementResult(MoneyPlacementOutcome.QuantityOutOfRange, onHandMoney, tradeOfferMoney);

        if (amount > onHandMoney)
            return new MoneyPlacementResult(MoneyPlacementOutcome.InsufficientSourceBalance, onHandMoney,
                tradeOfferMoney);

        var newTradeOfferMoney = tradeOfferMoney + amount;
        if (newTradeOfferMoney > MoneyCeiling)
            return new MoneyPlacementResult(MoneyPlacementOutcome.DestinationOverflow, onHandMoney, tradeOfferMoney);

        return new MoneyPlacementResult(MoneyPlacementOutcome.Success, onHandMoney - amount, newTradeOfferMoney);
    }

    /// <summary>tSort 222 -- ProcessForTradeMoneyToInventoryMoney: trade-offer money moves back to on-hand.</summary>
    public static MoneyPlacementResult ResolveFromTradeOffer(long tradeOfferMoney, long onHandMoney, long amount)
    {
        if (amount < 1)
            return new MoneyPlacementResult(MoneyPlacementOutcome.QuantityOutOfRange, onHandMoney, tradeOfferMoney);

        if (amount > tradeOfferMoney)
            return new MoneyPlacementResult(MoneyPlacementOutcome.InsufficientSourceBalance, onHandMoney,
                tradeOfferMoney);

        var newOnHandMoney = onHandMoney + amount;
        if (newOnHandMoney > MoneyCeiling)
            return new MoneyPlacementResult(MoneyPlacementOutcome.DestinationOverflow, onHandMoney, tradeOfferMoney);

        return new MoneyPlacementResult(MoneyPlacementOutcome.Success, newOnHandMoney, tradeOfferMoney - amount);
    }

    /// <summary>NewOnHandMoney/NewTradeOfferMoney are both sides' post-move balances, valid only when Succeeded.</summary>
    public readonly record struct MoneyPlacementResult(
        MoneyPlacementOutcome Outcome,
        long NewOnHandMoney,
        long NewTradeOfferMoney)
    {
        public bool Succeeded => Outcome == MoneyPlacementOutcome.Success;
    }
}

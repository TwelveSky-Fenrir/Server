using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Social.Trade;

/// <summary>
///     Pure resolver for the trade-window "1B"/"1 Md" BigMoney placement family: moving on-hand BigMoney into
///     the caller's trade offer (<see cref="ResolveToTradeOffer" />, tSort 240) and back out
///     (<see cref="ResolveFromTradeOffer" />, tSort 243). No I/O -- independently unit-testable.
///     <para>
///         Unlike <see cref="TradeMoneyPlacementResolver" /> (tSort 221/222, the ordinary-money counterpart),
///         both directions here are additionally gated by a trade-lock guard: once the caller has registered at
///         least one confirmation click on their own side of the currently open trade window
///         (<c>TradeOfferSide.MenuState</c> &gt;= 1, the two-stage confirm sequence 0 unconfirmed / 1 locked / 2
///         confirmed), neither direction may run. This guard does not exist for the Store/Bank BigMoney pairs --
///         see <see cref="Fenrir.Application.Game.Domain.Inventory.BigMoneyTransferPolicy" /> -- and the caller
///         is expected to only invoke this resolver at all while a trade window is actually open (there is no
///         "no trade open" input here, mirroring <see cref="TradeMoneyPlacementResolver" />'s own posture).
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3582-3622 (<c>ProcessFor1BInventoryMoneyTo1BTradeMoney</c>,
///     tSort 240: trade-lock guard, symmetric quantity mutation, <c>ProcessForTradeInfo()</c> call) ; :3624-3664
///     (<c>ProcessFor1BTradeMoneyTo1BInventoryMoney</c>, tSort 243: same trade-lock guard in the reverse
///     direction) ; Server/Header/Protocol/DEFINE.h:365-367 (<c>MAX_NUMBER_SIZE2</c> = 999, the BigMoney cap) ;
///     Server/ts25zone/S04_MyWork02.cpp:8629-8637 (<c>TRADE_START_SEND</c> setting both sides' confirmation-menu
///     state to unconfirmed) ; Server/ts25zone/S04_MyWork02.cpp:8726-8767 (<c>TRADE_MENU_SEND</c>
///     unconfirmed-&gt;one-side-confirmed-&gt;both-sides-confirmed transitions).
///     <para>
///         Out of scope by contract: the live push of the acting player's refreshed trade totals to their
///         partner's session (legacy <c>AVATAR_OBJECT::ProcessForTradeInfo</c>,
///         Server/ts25zone/S07_MyGame04.cpp:1660-1683) is a conditional, best-effort broadcast that depends on
///         locating and validating the partner's own session/state -- that is orchestration, not a pure rule,
///         and belongs in the Services-layer caller once this family is wired up, not in this resolver.
///     </para>
/// </remarks>
public static class TradeBigMoneyPlacementResolver
{
    /// <summary>MAX_NUMBER_SIZE2 (DEFINE.h:367) -- the same BigMoney cap every BigMoney-family destination uses.</summary>
    public const long BigMoneyCap = BigMoneyTransferPolicy.BigMoneyCap;

    /// <summary>TradeOfferSide.MenuState value at/above which the caller has locked their side of the trade.</summary>
    public const int LockedMenuState = 1;

    public enum BigMoneyPlacementOutcome
    {
        Success,

        /// <summary>
        ///     The caller has already registered at least one confirmation click on their own side of the
        ///     currently open trade window -- neither direction may adjust the offered BigMoney past this point.
        /// </summary>
        TradeLocked,

        /// <summary>Requested amount is less than 1.</summary>
        QuantityOutOfRange,

        /// <summary>The moving side's balance does not already hold at least the requested amount.</summary>
        InsufficientSourceBalance,

        /// <summary>The receiving side's balance, after adding the requested amount, would exceed <see cref="BigMoneyCap" />.</summary>
        DestinationOverflow
    }

    /// <summary>NewOnHandBigMoney/NewTradeOfferBigMoney are both sides' post-move balances, valid only when Succeeded.</summary>
    public readonly record struct BigMoneyPlacementResult(
        BigMoneyPlacementOutcome Outcome,
        long NewOnHandBigMoney,
        long NewTradeOfferBigMoney)
    {
        public bool Succeeded => Outcome == BigMoneyPlacementOutcome.Success;
    }

    /// <summary>
    ///     tSort 240 -- ProcessFor1BInventoryMoneyTo1BTradeMoney: on-hand BigMoney moves into the trade offer.
    /// </summary>
    /// <param name="ownMenuState">The caller's own <c>TradeOfferSide.MenuState</c> in the currently open trade.</param>
    public static BigMoneyPlacementResult ResolveToTradeOffer(
        int ownMenuState, long onHandBigMoney, long tradeOfferBigMoney, long amount)
    {
        if (ownMenuState >= LockedMenuState)
            return new BigMoneyPlacementResult(BigMoneyPlacementOutcome.TradeLocked, onHandBigMoney,
                tradeOfferBigMoney);

        if (amount < 1)
            return new BigMoneyPlacementResult(BigMoneyPlacementOutcome.QuantityOutOfRange, onHandBigMoney,
                tradeOfferBigMoney);

        if (amount > onHandBigMoney)
            return new BigMoneyPlacementResult(BigMoneyPlacementOutcome.InsufficientSourceBalance, onHandBigMoney,
                tradeOfferBigMoney);

        var newTradeOfferBigMoney = tradeOfferBigMoney + amount;
        if (newTradeOfferBigMoney > BigMoneyCap)
            return new BigMoneyPlacementResult(BigMoneyPlacementOutcome.DestinationOverflow, onHandBigMoney,
                tradeOfferBigMoney);

        return new BigMoneyPlacementResult(BigMoneyPlacementOutcome.Success, onHandBigMoney - amount,
            newTradeOfferBigMoney);
    }

    /// <summary>
    ///     tSort 243 -- ProcessFor1BTradeMoneyTo1BInventoryMoney: trade-offer BigMoney moves back to on-hand.
    /// </summary>
    /// <param name="ownMenuState">The caller's own <c>TradeOfferSide.MenuState</c> in the currently open trade.</param>
    public static BigMoneyPlacementResult ResolveFromTradeOffer(
        int ownMenuState, long tradeOfferBigMoney, long onHandBigMoney, long amount)
    {
        if (ownMenuState >= LockedMenuState)
            return new BigMoneyPlacementResult(BigMoneyPlacementOutcome.TradeLocked, onHandBigMoney,
                tradeOfferBigMoney);

        if (amount < 1)
            return new BigMoneyPlacementResult(BigMoneyPlacementOutcome.QuantityOutOfRange, onHandBigMoney,
                tradeOfferBigMoney);

        if (amount > tradeOfferBigMoney)
            return new BigMoneyPlacementResult(BigMoneyPlacementOutcome.InsufficientSourceBalance, onHandBigMoney,
                tradeOfferBigMoney);

        var newOnHandBigMoney = onHandBigMoney + amount;
        if (newOnHandBigMoney > BigMoneyCap)
            return new BigMoneyPlacementResult(BigMoneyPlacementOutcome.DestinationOverflow, onHandBigMoney,
                tradeOfferBigMoney);

        return new BigMoneyPlacementResult(BigMoneyPlacementOutcome.Success, newOnHandBigMoney,
            tradeOfferBigMoney - amount);
    }
}

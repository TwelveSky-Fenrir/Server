using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Social.Trade;

public static class TradeBigMoneyPlacementResolver
{
    public enum BigMoneyPlacementOutcome
    {
        Success,

                TradeLocked,

                QuantityOutOfRange,

                InsufficientSourceBalance,

                DestinationOverflow
    }

        public const long BigMoneyCap = BigMoneyTransferPolicy.BigMoneyCap;

        public const int LockedMenuState = 1;

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

        public readonly record struct BigMoneyPlacementResult(
        BigMoneyPlacementOutcome Outcome,
        long NewOnHandBigMoney,
        long NewTradeOfferBigMoney)
    {
        public bool Succeeded => Outcome == BigMoneyPlacementOutcome.Success;
    }
}

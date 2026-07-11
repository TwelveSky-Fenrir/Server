namespace Fenrir.Application.Game.Domain.Social.Trade;

public static class TradeMoneyPlacementResolver
{
    public enum MoneyPlacementOutcome
    {
        Success,

                QuantityOutOfRange,

                InsufficientSourceBalance,

                DestinationOverflow
    }

        public const long MoneyCeiling = 2_000_000_000;

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

        public readonly record struct MoneyPlacementResult(
        MoneyPlacementOutcome Outcome,
        long NewOnHandMoney,
        long NewTradeOfferMoney)
    {
        public bool Succeeded => Outcome == MoneyPlacementOutcome.Success;
    }
}

namespace Fenrir.Application.Game.Domain.Inventory;

public static class BigMoneyUnitConversionPolicy
{
    public enum BigMoneyUnitConversionOutcome
    {
        Success,

                QuantityBelowMinimum,

                InsufficientSourceBalance,

                DestinationOverflow
    }

        public const long MoneyCeiling = 2_000_000_000;

        public const long BigMoneyUnitValue = 1_000_000_000;

        public const long BigMoneyCap = 999;

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

        public readonly record struct BigMoneyUnitConversionResult(
        BigMoneyUnitConversionOutcome Outcome,
        long NewInventoryMoney,
        long NewInventoryBigMoney)
    {
        public bool Succeeded => Outcome == BigMoneyUnitConversionOutcome.Success;
    }
}

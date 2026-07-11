using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Consumables;

public static class CpTicketResolver
{
    public enum Outcome
    {
        Success,

                InsufficientQuantity,

                WouldExceedCeiling
    }

    private static readonly FrozenDictionary<int, int> PerUnitAmounts = new Dictionary<int, int>
    {
        [691] = 5,
        [692] = 10,
        [693] = 15,
        [694] = 20,
        [1444] = 500,
        [1447] = 1000,
        [8433] = 1000,
        [1448] = 500,
        [8432] = 500,
        [1449] = 100,
        [8431] = 100,
        [1499] = 5000,
        [8434] = 5000
    }.ToFrozenDictionary();

        public static IEnumerable<int> HandledItemIds => PerUnitAmounts.Keys;

    public static bool TryGetPerUnitAmount(int itemId, out int perUnitAmount)
    {
        return PerUnitAmounts.TryGetValue(itemId, out perUnitAmount);
    }

        public static Result Resolve(int itemId, int requestedValue, int stackQuantity, int currentContributionPoints)
    {
        if (stackQuantity < 1 || !PerUnitAmounts.TryGetValue(itemId, out var perUnitAmount))
            return new Result(Outcome.InsufficientQuantity, 0, 0, currentContributionPoints);

        var bulkCount = BulkUseCoercion.Coerce(requestedValue, stackQuantity);
        var totalAmount = (long)perUnitAmount * bulkCount;
        var added = BankedCounterMath.AddWideSafe(currentContributionPoints, totalAmount);

        return added.Succeeded
            ? new Result(Outcome.Success, bulkCount, (int)totalAmount, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, 0, 0, currentContributionPoints);
    }

    public readonly record struct Result(
        Outcome Outcome,
        int UnitsConsumed,
        int CreditedAmount,
        int NewContributionPoints)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}

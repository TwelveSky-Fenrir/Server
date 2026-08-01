namespace Fenrir.Application.Game.Domain.Consumables;

public static class BankedCounterMath
{
    public enum AddOutcome
    {
        Success,

        WouldExceedCeiling
    }

    public const int GlobalCeiling = 2_000_000_000;

    public static AddResult AddWideSafe(int current, long amount, int ceiling = GlobalCeiling)
    {
        var projected = current + amount;
        return projected > ceiling
            ? new AddResult(AddOutcome.WouldExceedCeiling, current)
            : new AddResult(AddOutcome.Success, (int)projected);
    }

    public static AddResult AddNarrow(int current, int amount, int ceiling = GlobalCeiling)
    {
        unchecked
        {
            var projected = current + amount;
            return projected > ceiling
                ? new AddResult(AddOutcome.WouldExceedCeiling, current)
                : new AddResult(AddOutcome.Success, projected);
        }
    }

    public static int CoerceBulkToHeadroom(int current, int cap, int perUnitAmount, int requestedCount)
    {
        if (perUnitAmount <= 0 || requestedCount <= 0)
            return 0;

        var headroom = cap - current;
        if (headroom <= 0)
            return 0;

        var maxUnits = headroom / perUnitAmount;
        return Math.Min(requestedCount, maxUnits);
    }

    public readonly record struct AddResult(AddOutcome Outcome, int NewValue)
    {
        public bool Succeeded => Outcome == AddOutcome.Success;
    }
}

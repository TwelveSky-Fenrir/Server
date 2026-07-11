namespace Fenrir.Application.Game.Domain.Consumables;

public static class ScrollOfSeekersResolver
{
    public enum Outcome
    {
        Success,

                WouldExceedCeiling
    }

        public const int ScrollOfSeekersItemId = 1124;

        public const int ScrollOfSeekersLItemId = 1187;

        public const int ScrollOfSeekers15HourItemId = 7016;

        public const int ScrollOfSeekers3HourItemId = 8409;

        public const int ScrollOfSeekers15HourAltItemId = 8410;

        public static IEnumerable<int> HandledItemIds { get; } =
    [
        ScrollOfSeekersItemId,
        ScrollOfSeekersLItemId,
        ScrollOfSeekers15HourItemId,
        ScrollOfSeekers3HourItemId,
        ScrollOfSeekers15HourAltItemId
    ];

    public const int DefaultAddAmount = 180;
    public const int OverrideAddAmount = 900;

        public static int AmountFor(int itemId)
    {
        return itemId switch
        {
            ScrollOfSeekersLItemId or ScrollOfSeekers15HourItemId or ScrollOfSeekers15HourAltItemId =>
                OverrideAddAmount,
            _ => DefaultAddAmount
        };
    }

    public static Result Resolve(int itemId, int currentZoneTime)
    {
        var amount = AmountFor(itemId);
        var added = BankedCounterMath.AddNarrow(currentZoneTime, amount);
        return added.Succeeded
            ? new Result(Outcome.Success, amount, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, amount, currentZoneTime);
    }

    public readonly record struct Result(Outcome Outcome, int CreditedAmount, int NewZoneTime)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}

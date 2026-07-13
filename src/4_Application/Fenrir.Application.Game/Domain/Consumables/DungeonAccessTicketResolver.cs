namespace Fenrir.Application.Game.Domain.Consumables;

public static class DungeonAccessTicketResolver
{
    public enum Outcome
    {
        Success,

        InsufficientQuantity,

        WouldExceedCeiling
    }

    public const int EliteDungeonTicketLargeItemId = 1047;
    public const int EliteDungeonTicketMediumItemId = 1097;
    public const int EliteDungeonTicketSmallItemId = 1098;
    public const int DungeonKeyItemId = 1048;
    public const int IvyHallTicketSmallItemId = 553;
    public const int IvyHallTicketLargeItemId = 1219;

    public const int EliteDungeonTicketLargeAmount = 180;
    public const int EliteDungeonTicketMediumAmount = 120;
    public const int EliteDungeonTicketSmallAmount = 60;
    public const int DungeonKeyAmount = 1;
    public const int IvyHallTicketSmallAmount = 180;
    public const int IvyHallTicketLargeAmount = 360;

    public static Result Resolve(int currentCounter, int perUnitAmount, int slotQuantity,
        int ceiling = BankedCounterMath.GlobalCeiling)
    {
        if (slotQuantity < 1)
            return new Result(Outcome.InsufficientQuantity, currentCounter);

        var added = BankedCounterMath.AddNarrow(currentCounter, perUnitAmount, ceiling);
        return added.Succeeded
            ? new Result(Outcome.Success, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, currentCounter);
    }

    public readonly record struct Result(Outcome Outcome, int NewCounterValue)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}

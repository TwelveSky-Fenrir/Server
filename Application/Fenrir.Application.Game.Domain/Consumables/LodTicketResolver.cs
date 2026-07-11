using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Consumables;

public static class LodTicketResolver
{
    public enum Outcome
    {
        Success,

                PreconditionFailed,

                InsufficientQuantity,

                WouldExceedCeiling
    }

    public static Result Resolve(short characterLevel, int rebirthCount, int slotQuantity, int currentLodRounds)
    {
        if (characterLevel < LevelProgressionCalculator.MaxLevel || rebirthCount < 1)
            return new Result(Outcome.PreconditionFailed, currentLodRounds);

        if (slotQuantity < 1)
            return new Result(Outcome.InsufficientQuantity, currentLodRounds);

        var added = BankedCounterMath.AddWideSafe(currentLodRounds, 1);
        return added.Succeeded
            ? new Result(Outcome.Success, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, currentLodRounds);
    }

    public readonly record struct Result(Outcome Outcome, int NewLodRounds)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}

using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Consumables;

public static class CashTimerResolver
{
    public enum Outcome
    {
        Success,

        WouldExceedCeiling,

        LevelCapNotMet
    }

    public const int FactionNoticeAddAmount = 5;
    public const int TaiyanKeyAddAmount = 180;

    public static Result ResolveFactionNotice(int currentCount)
    {
        var added = BankedCounterMath.AddNarrow(currentCount, FactionNoticeAddAmount);
        return added.Succeeded
            ? new Result(Outcome.Success, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, currentCount);
    }

    public static Result ResolveTaiyanKey(short characterLevel, int currentTimer)
    {
        if (characterLevel < LevelProgressionCalculator.MaxLevel)
            return new Result(Outcome.LevelCapNotMet, currentTimer);

        var added = BankedCounterMath.AddNarrow(currentTimer, TaiyanKeyAddAmount);
        return added.Succeeded
            ? new Result(Outcome.Success, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, currentTimer);
    }

    public readonly record struct Result(Outcome Outcome, int NewValue)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}

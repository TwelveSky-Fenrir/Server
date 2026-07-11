using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Forge;

public static class RefineResolver
{
    public enum RefineOutcome
    {
        Rejected,
        Success,
        Failed
    }

        public const int MaxRefine = 25;

        public static int RefineRate(int currentRefine, int addedLevel)
    {
        var rate = 72 - 2 * (currentRefine + addedLevel);
        return Math.Clamp(rate, 0, 100);
    }

        public static RefineResult Resolve(int currentRefine, int addedLevel, IRandomSource random)
    {
        if (addedLevel <= 0 || currentRefine < 0 || currentRefine >= MaxRefine)
            return new RefineResult(RefineOutcome.Rejected, currentRefine, 0);

        var rate = RefineRate(currentRefine, addedLevel);

        if (random.NextInt32(100) < rate)
            return new RefineResult(RefineOutcome.Success, Math.Min(currentRefine + addedLevel, MaxRefine), rate);

        return new RefineResult(RefineOutcome.Failed, currentRefine, rate);
    }

    public readonly record struct RefineResult(RefineOutcome Outcome, int NewRefine, int Rate);
}

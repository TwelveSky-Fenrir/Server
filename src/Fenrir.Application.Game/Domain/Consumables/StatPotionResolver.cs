namespace Fenrir.Application.Game.Domain.Consumables;

public static class StatPotionResolver
{
    private const int TenStackAmount = 10;

    public enum Outcome
    {
        Success,

        AlreadyMaxed,

        PreconditionFailed
    }

    public const int OrdinaryTierCap = 200;
    public const int G12TierCap = 400;

    public const int RequiredLevel2 = 12;

    public static OrdinaryResult ResolveOrdinary(int currentCount, int perUnitAmount, int requestedBulkCount)
    {
        if (perUnitAmount == TenStackAmount)
        {
            if (currentCount >= OrdinaryTierCap)
                return new OrdinaryResult(Outcome.AlreadyMaxed, currentCount, 0);

            var availableUnits = (OrdinaryTierCap - currentCount) / TenStackAmount;
            if (requestedBulkCount <= 0 || requestedBulkCount > availableUnits)
                return new OrdinaryResult(Outcome.PreconditionFailed, currentCount, 0);

            return new OrdinaryResult(Outcome.Success, currentCount + TenStackAmount * requestedBulkCount,
                requestedBulkCount);
        }

        var coercedCount =
            BankedCounterMath.CoerceBulkToHeadroom(currentCount, OrdinaryTierCap, perUnitAmount, requestedBulkCount);

        if (coercedCount <= 0)
            return new OrdinaryResult(Outcome.AlreadyMaxed, currentCount, 0);

        return new OrdinaryResult(Outcome.Success, currentCount + perUnitAmount * coercedCount, coercedCount);
    }

    public static G12Result ResolveG12(int currentCount, int level2, int requestedBulkCount)
    {
        if (currentCount < OrdinaryTierCap || currentCount >= G12TierCap || level2 < RequiredLevel2)
            return new G12Result(Outcome.PreconditionFailed, currentCount, 0);

        var headroom = G12TierCap - currentCount;
        var coercedCount = Math.Min(Math.Max(requestedBulkCount, 1), headroom);

        return new G12Result(Outcome.Success, currentCount + coercedCount, coercedCount);
    }

    public readonly record struct OrdinaryResult(Outcome Outcome, int NewCount, int UnitsConsumed)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }

    public readonly record struct G12Result(Outcome Outcome, int NewCount, int UnitsConsumed)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}

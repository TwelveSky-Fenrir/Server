namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum TribeQuotaGroup : byte
{

        None = 0,

        ThreeWay = 1,

        FourWay = 2,

        RvrInstancedNoGate = 3
}

public enum TribeQuotaOutcome
{

        Accepted,

        QuotaFull
}

public static class TribeQuotaGate
{

        public static bool IsDeclaredTribeInRange(TribeQuotaGroup group, int declaredTribe)
    {
        return group switch
        {
            TribeQuotaGroup.ThreeWay => declaredTribe is >= 0 and <= 2,
            TribeQuotaGroup.FourWay => declaredTribe is >= 0 and <= 3,
            _ => true
        };
    }

        public static TribeQuotaOutcome Evaluate(TribeQuotaGroup group, int maxConcurrentConnections,
        int currentPopulationForDeclaredTribe)
    {
        var divisor = group switch
        {
            TribeQuotaGroup.ThreeWay => 3,
            TribeQuotaGroup.FourWay => 4,
            _ => 0
        };

        if (divisor == 0)
            return TribeQuotaOutcome.Accepted;

        var threshold = maxConcurrentConnections / divisor;
        return currentPopulationForDeclaredTribe >= threshold
            ? TribeQuotaOutcome.QuotaFull
            : TribeQuotaOutcome.Accepted;
    }
}

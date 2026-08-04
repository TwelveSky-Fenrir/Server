namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum TribeQuotaGroup : byte
{
    None = 0,

    ThreeWay = 1,

    FourWay = 2
}

public static class TribeQuotaGroupPolicy
{
    public static TribeQuotaGroup ForMap(short mapId)
    {
        return mapId switch
        {
            49 or 51 or 53 or >= 146 and <= 153 or 267 => TribeQuotaGroup.ThreeWay,
            >= 154 and <= 164 or >= 120 and <= 122 or 194 or 200 or 268 or 269 or 295 or 296 =>
                TribeQuotaGroup.FourWay,
            _ => TribeQuotaGroup.None
        };
    }
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

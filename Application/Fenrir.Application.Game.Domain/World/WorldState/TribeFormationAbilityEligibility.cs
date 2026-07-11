namespace Fenrir.Application.Game.Domain.World.WorldState;

public static class TribeFormationAbilityEligibility
{
    public const int PointFloor = 100;

    public const int SharePercentThreshold = 20;

    public static bool AllTribesAboveFloor(IReadOnlyList<TribeRvrState> tribes)
    {
        foreach (var tribe in tribes)
            if (tribe.Points <= PointFloor)
                return false;

        return true;
    }

    public static byte FindLowestPointTribe(IReadOnlyList<TribeRvrState> tribes)
    {
        var lowest = tribes[0];
        for (var i = 1; i < tribes.Count; i++)
            if (tribes[i].Points < lowest.Points)
                lowest = tribes[i];

        return lowest.TribeId;
    }

    public static int CombinedPoints(IReadOnlyList<TribeRvrState> tribes)
    {
        var total = 0;
        foreach (var tribe in tribes)
            total += tribe.Points;

        return total;
    }

    public static bool IsUnderShareThreshold(int ownPoints, int combinedPoints)
    {
        var sharePercent = ownPoints * 100 / combinedPoints;
        return sharePercent < SharePercentThreshold;
    }
}

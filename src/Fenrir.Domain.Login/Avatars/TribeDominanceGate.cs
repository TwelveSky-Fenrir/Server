namespace Fenrir.Domain.Login.Avatars;

public static class TribeDominanceGate
{
    public const int DominantTribeFloor = 100;

    public const int TribeSlotCount = 4;

    public static bool BlocksCreation(byte requestedTribe, IReadOnlyList<TribeSummaryDto> standings)
    {
        var pointsByTribe = new int[TribeSlotCount];

        foreach (var standing in standings)
            if (standing.TribeId < TribeSlotCount)
                pointsByTribe[standing.TribeId] = standing.Points.GetValueOrDefault();

        var leadingTribe = 0;
        for (var tribeId = 1; tribeId < TribeSlotCount; tribeId++)
            if (pointsByTribe[tribeId] > pointsByTribe[leadingTribe])
                leadingTribe = tribeId;

        return pointsByTribe[leadingTribe] >= DominantTribeFloor && leadingTribe == requestedTribe;
    }
}

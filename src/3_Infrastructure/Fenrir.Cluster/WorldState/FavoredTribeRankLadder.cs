namespace Fenrir.Cluster.WorldState;

public static class FavoredTribeRankLadder
{
    public const int TribeCount = 4;
    public const int Baseline = 1000;
    public const int FavoredTribeBonus = 4000;

    private static readonly int[] DistanceBonus = [0, 100, 200, 300];

    public static int[] ComputeTotals(byte favoredTribeId)
    {
        if (favoredTribeId >= TribeCount)
            throw new ArgumentOutOfRangeException(nameof(favoredTribeId), favoredTribeId,
                $"TribeId must be 0-{TribeCount - 1}.");

        var totals = new int[TribeCount];
        for (byte tribeId = 0; tribeId < TribeCount; tribeId++)
        {
            var distance = (tribeId - favoredTribeId + TribeCount) % TribeCount;
            totals[tribeId] = Baseline + DistanceBonus[distance];
        }

        totals[favoredTribeId] += FavoredTribeBonus;
        return totals;
    }

    public static byte NextFavoredTribe(byte? current)
    {
        var next = current is { } value ? (value + 1) % TribeCount : 0;
        return (byte)next;
    }
}

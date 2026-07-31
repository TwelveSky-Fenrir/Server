namespace Fenrir.Cluster.Center.WorldState;

public static class TribeScoreRecompute
{
    public const int TribeCount = FavoredTribeRankLadder.TribeCount;
    public const int Baseline = 1000;
    public const byte SeaBonusTribe = 3;
    public const int SeaBonus = 800;

    public static int[] ToScores(IReadOnlyList<CenterTribeStatAggregate> aggregates)
    {
        var scores = new int[TribeCount];
        for (var i = 0; i < TribeCount; i++)
            scores[i] = Baseline;

        foreach (var aggregate in aggregates)
        {
            if (aggregate.TribeId >= TribeCount)
                continue;

            scores[aggregate.TribeId] = Baseline + checked((int)aggregate.StatSum);
        }

        scores[SeaBonusTribe] += SeaBonus;
        return scores;
    }
}

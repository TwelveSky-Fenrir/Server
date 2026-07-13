namespace Fenrir.Cluster.WorldState;

/// <summary>
///     The Fenrir-side finalisation of the 6-beat tribe-score recompute (Bug 3): applies the 1000 baseline to
///     every tribe and the flat +800 to tribe 3 on top of the per-tribe stat-sum aggregates read from the
///     character-stats source.
/// </summary>
/// <remarks>
///     Reimplemented from Server/ts25center/S08_MyDB.cpp:108-135. The +800 for tribe index 3 is live because the
///     SEA-server macro is inactive (DEFINE.h:5). The per-character stat sum itself
///     (<c>(level1-112) + (level2*3) + (rebirth*3)</c> over level&gt;=145 characters) is computed in SQL by the
///     character-stats source (<see cref="ICenterTribeScoreSource" />); this class only layers the constants.
/// </remarks>
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

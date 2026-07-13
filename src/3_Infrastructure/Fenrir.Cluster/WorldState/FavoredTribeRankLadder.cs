namespace Fenrir.Cluster.WorldState;

/// <summary>
///     The advantaged-tribe ("high tribe") score distribution applied on the weekly rotation (Bug 2 fix) and on
///     the manual operator recompute. Every tribe starts at a 1000 baseline, gains a rank bonus by cyclic
///     distance from the favored tribe, and the favored tribe itself gains a flat +4000.
/// </summary>
/// <remarks>
///     Reimplemented from the advantaged-tribe rotation value tables (Server/ts25center/S08_MyDB.cpp:144-191):
///     index 0 adds [0,100,200,300] to tribes [0,1,2,3], index 1 adds [300,0,100,200], index 2 adds
///     [200,300,0,100], index 3 adds [100,200,300,0] -- a cyclic distance ladder -- then +4000 to the favored
///     tribe. This is a numeric copy of the shard-side <c>FavoredTribeRankBonusLadder</c>; kept independent
///     because Fenrir.Cluster must not reference Fenrir.Application.
/// </remarks>
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

    /// <summary>Advances the favored tribe index one step in the 0 -&gt; 1 -&gt; 2 -&gt; 3 -&gt; 0 weekly cycle.</summary>
    public static byte NextFavoredTribe(byte? current)
    {
        var next = current is { } value ? (value + 1) % TribeCount : 0;
        return (byte)next;
    }
}

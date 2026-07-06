namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     Pure per-tribe totals formula for the favored-tribe rank-bonus ladder: every tribe resets to a flat 1000
///     baseline, then receives a bonus tier keyed by its cyclic forward distance from the favored tribe (the
///     favored tribe's own distance is 0: distance 0 -&gt; +0, 1 -&gt; +100, 2 -&gt; +200, 3 -&gt; +300, tribe
///     indices wrapping around the fixed set of 4), and finally the favored tribe alone receives one further
///     flat <see cref="FavoredTribeBonus" /> on top of whatever it already holds from the baseline/tier step
///     (so the favored tribe ends at baseline + 0 + 4000; every other tribe ends at baseline + its own
///     distance tier only). This does NOT itself advance/rotate which tribe is favored -- see
///     <see cref="FavoredTribeRankBonusLadderService" />'s own remarks for why that path is dead in the live
///     call chain.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S08_MyDB.cpp:138-193 (<c>GetTribePoint(BOOL bUpdateWeek)</c>, the active
///     <c>#ifndef USE_NEW_CLAN_POINT</c> branch) -- 1000 baseline, the four-case bonus-tier table (lines
///     157-183, equivalent to the cyclic-distance rule described above), and the favored tribe's own +4000
///     bonus (line 184) ; Server/Header/Protocol/DEFINE.h:92 (<c>//#define USE_NEW_CLAN_POINT</c>, commented
///     out) -- confirms this is the branch that compiles in every build.
/// </remarks>
public static class FavoredTribeRankBonusLadder
{
    public const int Baseline = 1000;
    public const int FavoredTribeBonus = 4000;

    /// <summary>Cyclic forward distance 0/1/2/3 from the favored tribe -&gt; bonus tier, per the legacy's four-case table.</summary>
    private static readonly int[] DistanceBonus = [0, 100, 200, 300];

    /// <summary>
    ///     Computes all 4 tribes' totals for one favored tribe. <paramref name="favoredTribeId" /> must be 0-3;
    ///     every one of the 4 possible values produces a fully-defined assignment (no reachable "invalid
    ///     favored tribe" state through this formula).
    /// </summary>
    public static int[] ComputeTotals(byte favoredTribeId)
    {
        if (favoredTribeId >= WorldStateService.TribeCount)
            throw new ArgumentOutOfRangeException(nameof(favoredTribeId), favoredTribeId,
                $"TribeId must be 0-{WorldStateService.TribeCount - 1}.");

        var totals = new int[WorldStateService.TribeCount];
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
        {
            var distance = (tribeId - favoredTribeId + WorldStateService.TribeCount) % WorldStateService.TribeCount;
            totals[tribeId] = Baseline + DistanceBonus[distance];
        }

        totals[favoredTribeId] += FavoredTribeBonus;
        return totals;
    }
}

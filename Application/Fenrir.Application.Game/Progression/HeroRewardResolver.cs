using Fenrir.Data.Progression;

namespace Fenrir.Application.Game.Progression;

/// <summary>
///     Port of CZ_HEROREWARD_SEND's ranked-slot lookup (S04_MyWork02.cpp:14178-14245): searches only the
///     caller's own live tribe among the Previous-period standings, exactly as
///     <c>mRankInfo-&gt;hCharIdx[myTribe][rank]</c> does -- a character ranked under a since-abandoned
///     tribe is unreachable, matching the legacy exactly.
/// </summary>
public static class HeroRewardResolver
{
    public enum Outcome
    {
        NotRanked,
        AlreadyClaimed,
        Claim
    }

    public const int SlotsPerTribe = HeroRankBuilder.SlotsPerTribe;

    /// <summary>LNW33's heroCp table (S04_MyWork02.cpp:14189-14199), index = rank (0 = #1 in the tribe).</summary>
    public static readonly int[] PointsByRank = [1000, 900, 800, 700, 600, 500, 400, 300, 200, 100];

    /// <param name="previousPeriodRowsOrderedByPointsDescending">usp_HeroRanking_GetByPeriod(1)'s own order.</param>
    public static Result Resolve(IReadOnlyList<HeroRankingRowDto> previousPeriodRowsOrderedByPointsDescending,
        byte tribe, int characterId)
    {
        var rank = 0;
        foreach (var row in previousPeriodRowsOrderedByPointsDescending)
        {
            if (row.TribeId != tribe)
                continue;

            if (rank >= SlotsPerTribe)
                break;

            if (row.CharacterId == characterId)
                return new Result(row.RewardClaimed == true ? Outcome.AlreadyClaimed : Outcome.Claim, rank, row);

            rank++;
        }

        return new Result(Outcome.NotRanked, -1, default);
    }

    public readonly record struct Result(Outcome Outcome, int Rank, HeroRankingRowDto? Row);
}

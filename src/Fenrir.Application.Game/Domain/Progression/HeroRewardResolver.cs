namespace Fenrir.Application.Game.Domain.Progression;

public static class HeroRewardResolver
{
    public enum Outcome
    {
        NotRanked,
        AlreadyClaimed,
        Claim
    }

    public const int SlotsPerTribe = HeroRankBuilder.SlotsPerTribe;

    public static readonly int[] PointsByRank = [1000, 900, 800, 700, 600, 500, 400, 300, 200, 100];

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
            {
                var acceptState = HeroRankAcceptStateRules.FromClaimedFlag(row.RewardClaimed);
                var outcome = HeroRankAcceptStateRules.IsClaimable(acceptState)
                    ? Outcome.Claim
                    : Outcome.AlreadyClaimed;
                return new Result(outcome, rank, row);
            }

            rank++;
        }

        return new Result(Outcome.NotRanked, -1, default);
    }

    public readonly record struct Result(Outcome Outcome, int Rank, HeroRankingRowDto? Row);
}

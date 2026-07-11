using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public readonly record struct RegularWarParticipant(
    int CharacterId,
    byte Tribe,
    short Level,
    short RebirthTier,
    int RebirthCount);

public readonly record struct RegularWarRewardGrant(
    int CharacterId,
    bool? IsWinningSide,
    long MoneyAmount,
    int ExperienceAmount,
    int CpBonusAmount,
    int HeroRankPoints,
    int LeaderboardCpAmount,
    bool RequestItemDrop)
{

        public bool GrantParticipationCounter => true;
}

public static class RegularWarRewardCalculator
{

        public const int WinningHeroRankPoints = 10;

        public const int LosingOrDrawHeroRankPoints = 3;

    private static readonly ImmutableArray<int> LeaderboardCpByRank = [100, 50, 25];

        public static ImmutableArray<RegularWarRewardGrant> Compute(
        RegularWarOutcome outcome,
        byte? winningTribe,
        byte? allyOfWinningTribe,
        RegularWarMapConfig config,
        IReadOnlyList<RegularWarParticipant> participants,
        ImmutableArray<int> topKillers,
        IRegularWarRewardValueProvider rewardValues)
    {
        if (outcome == RegularWarOutcome.AbortedEmptyMap || participants.Count == 0)
            return [];

        var builder = ImmutableArray.CreateBuilder<RegularWarRewardGrant>(participants.Count);

        foreach (var participant in participants)
        {
            var leaderboardCp = LeaderboardCpAmount(topKillers, participant.CharacterId);

            if (outcome == RegularWarOutcome.Draw)
            {
                builder.Add(new RegularWarRewardGrant(
                    participant.CharacterId,
                    null,
                    0,
                    0,
                    0,
                    LosingOrDrawHeroRankPoints,
                    leaderboardCp,
                    false));
                continue;
            }

            var isWinningSide = participant.Tribe == winningTribe ||
                                (allyOfWinningTribe is { } ally && participant.Tribe == ally);

            var baseMoney = rewardValues.GetMoneyReward(participant.RebirthTier);
            var baseExperience = rewardValues.GetExperienceReward(participant.Level);

            var money = isWinningSide ? baseMoney : baseMoney / 2;
            var experience = isWinningSide ? baseExperience : baseExperience / 2;

            var cpBonus = 0;
            if (config.CpBonusRule is { } rule && rule.IsSatisfiedBy(participant.RebirthTier, participant.RebirthCount))
                cpBonus = isWinningSide ? rule.WinningSideAmount : rule.LosingSideAmount;

            builder.Add(new RegularWarRewardGrant(
                participant.CharacterId,
                isWinningSide,
                money > 0 ? money : 0,
                experience > 0 ? experience : 0,
                cpBonus,
                isWinningSide ? WinningHeroRankPoints : LosingOrDrawHeroRankPoints,
                leaderboardCp,
                true));
        }

        return builder.MoveToImmutable();
    }

    private static int LeaderboardCpAmount(ImmutableArray<int> topKillers, int characterId)
    {
        if (topKillers.IsDefaultOrEmpty)
            return 0;

        var rank = topKillers.IndexOf(characterId);
        return rank >= 0 && rank < LeaderboardCpByRank.Length ? LeaderboardCpByRank[rank] : 0;
    }
}

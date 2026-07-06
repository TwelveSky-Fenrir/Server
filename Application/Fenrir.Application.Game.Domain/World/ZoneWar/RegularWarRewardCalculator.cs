using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The subset of a participating avatar's state <see cref="RegularWarRewardCalculator" /> needs. Deliberately
///     not <c>PlayerRuntimeState</c> itself -- this class stays pure/I-O free like <see cref="RegularWarSchedule" />.
/// </summary>
/// <param name="RebirthTier"><c>aLevel2</c>, the 1-12 post-cap ladder (0 = not yet reached).</param>
/// <param name="RebirthCount"><c>aRebirthNum</c>, 0-6 -- a separate counter from <paramref name="RebirthTier" />.</param>
public readonly record struct RegularWarParticipant(
    int CharacterId,
    byte Tribe,
    short Level,
    short RebirthTier,
    int RebirthCount);

/// <summary>
///     One participant's computed Regular War reward. <see cref="MoneyAmount" />/<see cref="ExperienceAmount" />
///     are already zero when the source value was non-positive (per contract, "grant only if positive"); money
///     overflow itself is NOT re-checked here -- see this cluster's report for why that guard is deliberately
///     delegated to <c>Zone.QueueMoneyGrant</c>'s existing write-behind pipeline instead of duplicated here.
/// </summary>
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
    /// <summary>Always true for every grant this calculator produces -- every participant passed in qualifies.</summary>
    public bool GrantParticipationCounter => true;
}

/// <summary>
///     Pure reward computation for one concluded (non-aborted) Regular War: CP bonus by server+rebirth
///     criterion, money/experience via an injected <see cref="IRegularWarRewardValueProvider" /> (losing side
///     gets exactly half, integer division), hero-rank points, and the individual top-3 kill leaderboard CP --
///     every one of these except the two pluggable base-value lookups uses the literal numbers this cluster's
///     behavior contract specifies.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:5289-5413 (the full reward-payout participant loop) ;
///     :5333-5350 (server-120/164 CP bonus) ; :3092-3107 (allied-tribe win-side lookup, resolved by the caller
///     and passed in as <paramref name="allyOfWinningTribe" /> -- see
///     <see cref="Fenrir.Application.Game.Domain.World.WorldState.WorldStateService.GetAllyOf" />) ;
///     Server/ts25zone/S07_MyGame02.cpp:414-472 (<c>RW_Reward</c>, the top-3 leaderboard payout).
/// </remarks>
public static class RegularWarRewardCalculator
{
    /// <summary>Hero-rank points for the winning/allied side (contract: flat 10, no formula).</summary>
    public const int WinningHeroRankPoints = 10;

    /// <summary>Hero-rank points for the losing side, and for everyone on a draw (contract: flat 3).</summary>
    public const int LosingOrDrawHeroRankPoints = 3;

    private static readonly ImmutableArray<int> LeaderboardCpByRank = [100, 50, 25];

    /// <summary>
    ///     Computes one grant per <paramref name="participants" /> entry. Returns an empty array for
    ///     <see cref="RegularWarOutcome.AbortedEmptyMap" /> -- there is nobody to reward and no participant list
    ///     should ever be passed for that outcome, but this guards the call defensively regardless.
    /// </summary>
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

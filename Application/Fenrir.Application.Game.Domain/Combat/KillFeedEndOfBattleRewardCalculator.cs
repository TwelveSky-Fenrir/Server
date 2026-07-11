using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Pure, I/O-free computation of the end-of-battle top-1/2/3 Contribution Point reward (source contract
///     steps 9-11) from a <see cref="KillFeedLeaderboard" />'s current top-3 snapshot. Does not itself apply
///     any grant, check who is still present, or gate on anything --
///     <see cref="World.Zone.ApplyKillFeedEndOfBattleRewards" />
///     owns all of that; this class only answers "how much CP does each ranked character get."
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:414-472 (FFA vs. non-FFA split, FFA top CP 200/100/80,
///     non-FFA top CP 100/50/25 with a x2 doubling on zone-type-267, single-rank-per-player early return --
///     the last of which is automatically satisfied here since <see cref="KillFeedLeaderboard" /> only ever
///     holds one entry per distinct <see cref="KillFeedRankedEntry.CharacterId" />, so no character can ever
///     occupy two of the top-3 ranks simultaneously). The low/high level bound parameters the source contract
///     says the legacy routine accepts but never consults are correctly omitted here entirely -- see the
///     source contract's own Open Question 3.
/// </remarks>
public static class KillFeedEndOfBattleRewardCalculator
{
    /// <summary>
    ///     <paramref name="isFfaMap" /> and <paramref name="isZone267" /> are mutually exclusive by
    ///     construction in production (335 and 267 are different map ids) -- passing both true simply applies
    ///     the FFA table and ignores the doubling flag, since FFA already has its own distinct CP table.
    /// </summary>
    public static ImmutableArray<RankReward> ComputeRankRewards(ImmutableArray<KillFeedRankedEntry> topThree,
        bool isFfaMap, bool isZone267)
    {
        if (topThree.IsDefaultOrEmpty)
            return [];

        var (top1, top2, top3) = isFfaMap
            ? (KillFeedRewardConstants.FfaTop1ContributionPoints, KillFeedRewardConstants.FfaTop2ContributionPoints,
                KillFeedRewardConstants.FfaTop3ContributionPoints)
            : (KillFeedRewardConstants.NonFfaTop1ContributionPoints,
                KillFeedRewardConstants.NonFfaTop2ContributionPoints,
                KillFeedRewardConstants.NonFfaTop3ContributionPoints);

        if (!isFfaMap && isZone267)
        {
            top1 *= KillFeedRewardConstants.Zone267ContributionPointMultiplier;
            top2 *= KillFeedRewardConstants.Zone267ContributionPointMultiplier;
            top3 *= KillFeedRewardConstants.Zone267ContributionPointMultiplier;
        }

        ReadOnlySpan<int> amountByRank = [top1, top2, top3];

        var count = Math.Min(3, topThree.Length);
        var builder = ImmutableArray.CreateBuilder<RankReward>(count);
        for (var i = 0; i < count; i++)
            builder.Add(new RankReward(topThree[i].CharacterId, amountByRank[i]));

        return builder.MoveToImmutable();
    }

    /// <summary>One character's own share of the end-of-battle top-1/2/3 CP reward.</summary>
    public readonly record struct RankReward(int CharacterId, int ContributionPoints);
}

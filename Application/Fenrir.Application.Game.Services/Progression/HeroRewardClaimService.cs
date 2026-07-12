using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Progression;

public sealed class HeroRewardClaimService(IHeroRankingRepository heroRankings, ILogger<HeroRewardClaimService> logger)
    : IHeroRewardClaimService
{
    public async ValueTask<HeroRewardClaimResult> ClaimAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        var rows = await heroRankings.GetByPeriodAsync(1, cancellationToken);
        var resolved = HeroRewardResolver.Resolve(rows, state.Tribe, characterId);

        if (resolved is not { Outcome: HeroRewardResolver.Outcome.Claim, Row: not null })
            return new HeroRewardClaimResult(resolved.Outcome == HeroRewardResolver.Outcome.AlreadyClaimed
                ? HeroRewardClaimOutcome.AlreadyClaimed
                : HeroRewardClaimOutcome.NotRanked);

        var points = HeroRewardResolver.PointsByRank[resolved.Rank];

        await heroRankings.ClaimRewardAsync(characterId, 1, points, cancellationToken);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, state.ContributionPoints + points), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped hero-reward CP mirror for character {CharacterId} -- reward is already durably granted via usp_HeroRanking_ClaimReward, this only delays the in-memory session reflecting it",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} claimed hero-rank reward: rank {Rank} awards {Points} contribution points",
            characterId, resolved.Rank, points);

        return new HeroRewardClaimResult(HeroRewardClaimOutcome.Claimed);
    }
}

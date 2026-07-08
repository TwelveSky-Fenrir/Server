using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Progression;

/// <summary>
///     Business logic extracted from <c>HeroRewardClaimHandler</c> (CZ_HEROREWARD_SEND, opcode 119). The real
///     reward is CP -- ZC_HEROREWARD_RECV's item-drop fields are dead code in this build and always sent as 0
///     by the handler.
/// </summary>
public sealed class HeroRewardClaimService(IHeroRankingRepository heroRankings, ILogger<HeroRewardClaimService> logger)
    : IHeroRewardClaimService
{
    public async ValueTask<HeroRewardClaimResult> ClaimAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        var rows = await heroRankings.GetByPeriodAsync(1, cancellationToken);
        var resolved = HeroRewardResolver.Resolve(rows, state.Tribe, characterId);

        if (resolved is not { Outcome: HeroRewardResolver.Outcome.Claim, Row: { } row })
            return new HeroRewardClaimResult(resolved.Outcome == HeroRewardResolver.Outcome.AlreadyClaimed
                ? HeroRewardClaimOutcome.AlreadyClaimed
                : HeroRewardClaimOutcome.NotRanked);

        var points = HeroRewardResolver.PointsByRank[resolved.Rank];

        await heroRankings.MarkRewardClaimedAsync(characterId, 1, row.Points, row.TribeId, row.Level,
            cancellationToken);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, state.ContributionPoints + points), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped hero-reward CP mirror for character {CharacterId} -- unlike sibling handlers this is NOT self-healing, the DB reward-claim row is already committed",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} claimed hero-rank reward: rank {Rank} awards {Points} contribution points",
            characterId, resolved.Rank, points);

        return new HeroRewardClaimResult(HeroRewardClaimOutcome.Claimed);
    }
}

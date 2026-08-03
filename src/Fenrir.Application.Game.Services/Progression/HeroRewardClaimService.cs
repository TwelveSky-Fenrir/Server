using CaeriusNet.Exceptions;
using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Progression;

public sealed class HeroRewardClaimService(IHeroRankingRepository heroRankings, ILogger<HeroRewardClaimService> logger)
    : IHeroRewardClaimService
{
    private const int ContributionPointStatSort = 3;
    private const int AlreadyClaimedOrNotRankedErrorNumber = 50357;
    private const int UnknownCharacterErrorNumber = 50358;

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
        var newContributionPoints = Math.Max(0, state.ContributionPoints + points);

        try
        {
            await heroRankings.ClaimRewardAsync(characterId, 1, points, cancellationToken);
        }
        catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException
                 { Number: AlreadyClaimedOrNotRankedErrorNumber })
        {
            logger.LogInformation(
                "Character {CharacterId} hero-reward claim lost a cross-shard race: usp_HeroRanking_ClaimReward's guarded UPDATE found the reward already claimed",
                characterId);
            return new HeroRewardClaimResult(HeroRewardClaimOutcome.AlreadyClaimed);
        }
        catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException
                 { Number: UnknownCharacterErrorNumber })
        {
            logger.LogWarning(ex,
                "Character {CharacterId} hero-reward claim failed: usp_HeroRanking_ClaimReward found no matching character row for the contribution-points grant",
                characterId);
            return new HeroRewardClaimResult(HeroRewardClaimOutcome.NotRanked);
        }

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, newContributionPoints), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped hero-reward CP mirror for character {CharacterId} -- reward is already durably granted via usp_HeroRanking_ClaimReward, this only delays the in-memory session reflecting it",
                zone.MapId, characterId);

        state.Session.Send(new AvatarStatUpdateResponse
        {
            Sort = ContributionPointStatSort, Value = newContributionPoints, Value2 = 0
        });

        logger.LogInformation(
            "Character {CharacterId} claimed hero-rank reward: rank {Rank} awards {Points} contribution points",
            characterId, resolved.Rank, points);

        return new HeroRewardClaimResult(HeroRewardClaimOutcome.Claimed);
    }
}

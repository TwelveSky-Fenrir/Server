using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Progression;

public sealed class HeroRewardClaimHandler(
    IHeroRewardClaimService heroRewardClaimService,
    ILogger<HeroRewardClaimHandler> logger)
    : IAsyncPacketHandler<HeroRewardClaimRequest>
{
    public async ValueTask HandleAsync(HeroRewardClaimRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: HeroRewardClaimRequest (op119) received for character {CharacterId}",
            session.SessionId, characterId);

        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await heroRewardClaimService.ClaimAsync(characterId, zone, state, cancellationToken);

            switch (result.Outcome)
            {
                case HeroRewardClaimOutcome.AlreadyClaimed:
                    logger.LogInformation("Hero-reward claim denied for character {CharacterId}: already claimed",
                        characterId);
                    session.Send(EmptyResponse(3));
                    break;
                case HeroRewardClaimOutcome.Claimed:
                    session.Send(EmptyResponse(1000));
                    break;
                case HeroRewardClaimOutcome.NotRanked:
                default:
                    logger.LogDebug("Hero-reward claim ignored for character {CharacterId}: not ranked",
                        characterId);
                    break;
            }
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private static HeroRewardClaimResponse EmptyResponse(int result)
    {
        return new HeroRewardClaimResponse
        {
            Result = result, Page = 0, Index1 = 0, Index2 = 0, Xy1 = 0, Xy2 = 0, ItemIndex = new int[8]
        };
    }
}

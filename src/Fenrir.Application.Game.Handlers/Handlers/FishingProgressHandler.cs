using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class FishingProgressHandler(
    IFishingProgressService fishingProgressService,
    ILogger<FishingProgressHandler> logger)
    : IInlinePacketHandler<FishingProgressRequest>
{
    public void Handle(in FishingProgressRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: FishingProgressRequest (op104) received for character {CharacterId}, sort {Sort}",
                session.SessionId, characterId, packet.Sort);

        if (zone.MapId != FishingLineHandler.FishingZoneNumber)
        {
            logger.LogWarning(
                "Fishing-progress request rejected for character {CharacterId}: map {MapId} is not the fishing zone -- aborting session",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        FishingProgressResult? result;
        switch (packet.Sort)
        {
            case 1:
                result = fishingProgressService.PollBite(zone, state, characterId);
                break;
            case 2:
                result = fishingProgressService.Recast(zone, state, characterId);
                logger.LogInformation("Character {CharacterId} recast fishing line", characterId);
                break;
            case 3:
                if (packet.FishingStep is < 0 or > 5)
                {
                    logger.LogWarning(
                        "Fishing-progress request rejected for character {CharacterId}: invalid step {FishingStep} -- aborting session",
                        characterId, packet.FishingStep);
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                }

                result = fishingProgressService.ForceStep(zone, state, characterId, packet.FishingStep);
                break;
            default:
                logger.LogWarning(
                    "Fishing-progress request rejected for character {CharacterId}: invalid sort {Sort} -- aborting session",
                    characterId, packet.Sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }

        if (result is not { } value)
            return;

        if (value.FishingStep is 4 or 5)
            logger.LogInformation("Character {CharacterId} fishing bite at step {FishingStep}", characterId,
                value.FishingStep);

        session.Send(new FishingProgressResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = value.ResultSort,
            FishingState = value.FishingState, FishingStep = value.FishingStep
        });
    }
}

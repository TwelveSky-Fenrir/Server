using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class FishingLineHandler(IFishingLineService fishingLineService, ILogger<FishingLineHandler> logger)
    : IInlinePacketHandler<FishingLineRequest>
{
    public const short FishingZoneNumber = 52;

    public void Handle(in FishingLineRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: FishingLineRequest (op103) received for character {CharacterId}, sort {Sort}",
            session.SessionId, characterId, packet.Sort);

        if (zone.MapId != FishingZoneNumber)
        {
            logger.LogDebug(
                "Fishing-line request ignored for character {CharacterId}: map {MapId} is not the fishing zone",
                characterId, zone.MapId);
            return;
        }

        FishingLineResult result;
        switch (packet.Sort)
        {
            case 1:
                result = fishingLineService.Cast(zone, state, characterId);
                logger.LogInformation("Character {CharacterId} cast fishing line (result {Result})", characterId,
                    result.Result);
                break;
            case 2:
                result = fishingLineService.Reel(zone, state, characterId);
                logger.LogInformation("Character {CharacterId} reeled fishing line (result {Result})", characterId,
                    result.Result);
                break;
            default:
                logger.LogWarning(
                    "Fishing-line request ignored for character {CharacterId}: invalid sort {Sort}",
                    characterId, packet.Sort);
                return;
        }

        session.Send(new FishingLineResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = result.Result,
            FishingState = result.FishingState, FishingStep = result.FishingStep
        });
    }
}

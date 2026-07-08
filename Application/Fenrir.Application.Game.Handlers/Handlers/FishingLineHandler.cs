using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_FISHING_STATE_SEND (opcode 103) -- zone 52 only (S04_MyWork01.cpp:115-122): any other zone would
///     never have registered this opcode in the legacy, so it disconnects here too. Sort 1=cast (gated by a
///     mesh check under the caster's own position), 2=reel; anything else disconnects.
/// </summary>
public sealed class FishingLineHandler(IFishingLineService fishingLineService, ILogger<FishingLineHandler> logger)
    : IInlinePacketHandler<FishingLineRequest>
{
    public const short FishingZoneNumber = 52;

    public void Handle(in FishingLineRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: FishingLineRequest (op103) received for character {CharacterId}, sort {Sort}",
            session.SessionId, characterId, packet.Sort);

        if (zone.MapId != FishingZoneNumber)
        {
            logger.LogWarning(
                "Fishing-line request rejected for character {CharacterId}: map {MapId} is not the fishing zone -- aborting session",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
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
                    "Fishing-line request rejected for character {CharacterId}: invalid sort {Sort} -- aborting session",
                    characterId, packet.Sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }

        session.Send(new FishingLineResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = result.Result,
            FishingState = result.FishingState, FishingStep = result.FishingStep
        });
    }
}

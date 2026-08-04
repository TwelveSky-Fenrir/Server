using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class FishingLineHandler(IFishingLineService fishingLineService, ILogger<FishingLineHandler> logger)
    : IAsyncPacketHandler<FishingLineRequest>
{
    public const short FishingZoneNumber = 52;

    public async ValueTask HandleAsync(FishingLineRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != FishingZoneNumber)
        {
            logger.LogDebug(
                "Fishing-line request on character {CharacterId}: map {MapId} does not accept op103",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort is not (1 or 2))
        {
            logger.LogDebug(
                "Fishing-line request (op103) on session {SessionId}: malformed sort {Sort}; closing without a response",
                session.SessionId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        FishingLineResult? result;
        if (packet.Sort == 1)
            result = await fishingLineService.CastAsync(zone, state, characterId, cancellationToken);
        else
            result = await fishingLineService.ReelAsync(zone, state, characterId, cancellationToken);

        if (result is not { } value)
        {
            logger.LogError("Zone {MapId} did not acknowledge fishing-line state for character {CharacterId}",
                zone.MapId, characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new FishingLineResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = value.Result,
            FishingState = value.FishingState, FishingStep = value.FishingStep
        });

        if (packet.Sort == 1 && value.Result == 1 &&
            (await zone.PostFishingCommandAndWaitForResultAsync(
                new FishingZoneCommand(characterId, 0, 0, false, true, 92, ApplyState: false), cancellationToken))
            .Kind !=
            ZoneCommandResultKind.Applied)
            logger.LogError("Zone {MapId} did not acknowledge fishing-line action for character {CharacterId}",
                zone.MapId, characterId);
    }
}

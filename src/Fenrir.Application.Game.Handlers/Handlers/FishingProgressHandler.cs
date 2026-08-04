using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class FishingProgressHandler(
    IFishingProgressService fishingProgressService,
    ILogger<FishingProgressHandler> logger)
    : IAsyncPacketHandler<FishingProgressRequest>
{
    public async ValueTask HandleAsync(FishingProgressRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != FishingLineHandler.FishingZoneNumber)
        {
            logger.LogDebug(
                "Fishing-progress request on character {CharacterId}: map {MapId} does not accept op104",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort is not (1 or 2 or 3))
        {
            logger.LogDebug(
                "Fishing-progress request (op104) on session {SessionId}: malformed sort {Sort}; closing without a response",
                session.SessionId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort == 3 && packet.FishingStep is < 0 or > 5)
        {
            logger.LogDebug(
                "Fishing-progress request (op104) on session {SessionId}: out-of-range step {FishingStep}; closing without a response",
                session.SessionId, packet.FishingStep);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        FishingProgressResult? result;
        var responseIsRequired = packet.Sort is 2 or 3 ||
                                 (state.FishingState != 0 && state.FishingStep == 3 &&
                                  state.FishingCastAtUtc is { } castAt &&
                                  DateTime.UtcNow - castAt >= TimeSpan.FromMinutes(1));
        switch (packet.Sort)
        {
            case 1:
                result = await fishingProgressService.PollBiteAsync(zone, state, characterId, cancellationToken);
                break;
            case 2:
                result = await fishingProgressService.RecastAsync(zone, state, characterId, cancellationToken);
                break;
            case 3:
                result = await fishingProgressService.ForceStepAsync(zone, state, characterId, packet.FishingStep,
                    cancellationToken);
                break;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }

        if (result is not { } value)
        {
            if (responseIsRequired)
            {
                logger.LogError("Zone {MapId} did not acknowledge fishing-progress state for character {CharacterId}",
                    zone.MapId, characterId);
                zoneSession.Abort(DisconnectReason.Faulted);
            }

            return;
        }

        session.Send(new FishingProgressResponse
        {
            ServerIndex = characterId, UniqueNumber = state.UniqueNumber, Result = value.ResultSort,
            FishingState = value.FishingState, FishingStep = value.FishingStep
        });

        if (value.BroadcastCapture &&
            (await zone.PostFishingCommandAndWaitForResultAsync(
                new FishingZoneCommand(characterId, 0, 0, false, true, 93, ApplyState: false), cancellationToken))
            .Kind !=
            ZoneCommandResultKind.Applied)
            logger.LogError("Zone {MapId} did not acknowledge fishing-capture action for character {CharacterId}",
                zone.MapId, characterId);
    }
}

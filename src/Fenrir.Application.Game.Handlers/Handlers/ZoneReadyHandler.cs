using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ZoneReadyHandler(IZoneReadyService service, ILogger<ZoneReadyHandler>? logger = null)
    : IInlinePacketHandler<ZoneReadyRequest>
{
    public void Handle(in ZoneReadyRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger?.LogDebug(
            "Session {SessionId}: ZoneReadyRequest (op13) received for character {CharacterId}, current state {State}, claimed tribe {ClaimedTribe}",
            session.SessionId, zoneSession.CharacterId, zoneSession.State, packet.Tribe);

        if (zoneSession.State != ZoneSessionState.Registering)
        {
            logger?.LogWarning(
                "Zone-ready request aborted for character {CharacterId} (session {SessionId}): invalid session state {State}",
                zoneSession.CharacterId, session.SessionId, zoneSession.State);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        if (zoneSession.CurrentZone is Zone zone && zoneSession.CharacterId is { } characterId &&
            zone.TryGetPlayer(characterId, out var state) && state is not null)
        {
            if (service.Validate(state, packet.Tribe, packet.AutoState) == ZoneReadyOutcome.Rejected)
            {
                logger?.LogWarning(
                    "Zone-ready handshake rejected for character {CharacterId} (session {SessionId}) -- aborting session",
                    characterId, session.SessionId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }
        }
        else
        {
            logger?.LogWarning(
                "Zone-ready handshake aborted for session {SessionId}: the registered character has no current player state",
                session.SessionId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        zoneSession.MarkInWorld();

        logger?.LogInformation(
            "Zone-ready handshake complete for character {CharacterId} (session {SessionId}) -- session is now InWorld",
            zoneSession.CharacterId, session.SessionId);
    }
}

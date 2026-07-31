using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Network.Dispatch.Sessions;
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

        if (zoneSession.State == ZoneSessionState.InWorld)
        {
            logger?.LogDebug(
                "Zone-ready request ignored for character {CharacterId} (session {SessionId}): session is already InWorld",
                zoneSession.CharacterId, session.SessionId);
            return;
        }

        if (zoneSession.CurrentZone is Zone zone && zoneSession.CharacterId is { } characterId &&
            zone.TryGetPlayer(characterId, out var state) && state is not null)
        {
            if (service.Validate(state, packet.Tribe, packet.AutoState) == ZoneReadyOutcome.Rejected)
            {
                logger?.LogWarning(
                    "Zone-ready handshake aborted for character {CharacterId} (session {SessionId})",
                    characterId, session.SessionId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }
        }
        else
        {
            logger?.LogDebug(
                "Zone-ready validation skipped for session {SessionId}: player state not yet ticked into the zone (benign staleness window)",
                session.SessionId);
        }

        zoneSession.MarkInWorld();

        logger?.LogInformation(
            "Zone-ready handshake complete for character {CharacterId} (session {SessionId}) -- session is now InWorld",
            zoneSession.CharacterId, session.SessionId);
    }
}

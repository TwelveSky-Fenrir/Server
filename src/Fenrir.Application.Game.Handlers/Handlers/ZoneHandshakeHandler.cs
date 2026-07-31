using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ZoneHandshakeHandler(
    IZoneHandshakeService service,
    SessionRegistry registry,
    ILogger<ZoneHandshakeHandler>? logger = null) : IAsyncPacketHandler<ZoneHandshakeRequest>
{
    public async ValueTask HandleAsync(ZoneHandshakeRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        logger?.LogDebug(
            "Session {SessionId}: ZoneHandshakeRequest (op11) received, declared tribe {DeclaredTribe}",
            session.SessionId, packet.Tribe);

        var result = await service.ConsumeTicketAsync(packet.Id, packet.Tribe, zoneSession, cancellationToken);

        switch (result.Outcome)
        {
            case ZoneHandshakeOutcome.QuotaFull:
                logger?.LogWarning("Zone handshake rejected for session {SessionId}: {Outcome}", session.SessionId,
                    result.Outcome);
                session.Send(new ZoneHandshakeResponse { Result = 1 });
                return;
            case ZoneHandshakeOutcome.Rejected:
                logger?.LogWarning("Zone handshake rejected for session {SessionId}: {Outcome}", session.SessionId,
                    result.Outcome);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case ZoneHandshakeOutcome.SessionSuperseded:
                logger?.LogWarning(
                    "Zone handshake aborted for session {SessionId}: superseded by a newer login for account {AccountId}",
                    session.SessionId, result.AccountId);
                zoneSession.Abort(DisconnectReason.Evicted);
                return;
            case ZoneHandshakeOutcome.ProtocolViolation:
                logger?.LogWarning("Zone handshake aborted for session {SessionId}: protocol violation",
                    session.SessionId);
                zoneSession.Abort(DisconnectReason.Malformed);
                return;
        }

        zoneSession.MarkTicketConsumed(result.AccountId, result.CharacterId, result.SessionToken,
            result.AccountGrade, result.TargetMapId);
        registry.AssociateAccount(session.SessionId, result.AccountId);
        session.Send(new ZoneHandshakeResponse { Result = 0 });

        logger?.LogInformation(
            "Zone handshake accepted for account {AccountId} character {CharacterId} (session {SessionId})",
            result.AccountId, result.CharacterId, session.SessionId);
    }
}

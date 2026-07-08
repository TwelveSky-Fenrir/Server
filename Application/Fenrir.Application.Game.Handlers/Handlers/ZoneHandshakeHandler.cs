using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op11, first packet after ZC_CONNECT_OK_RECV. Gates entry on this shard's tribe-population quota (if
///     any), then consumes the single-use session ticket the LoginServer minted for this AccountId (ADR-0005)
///     -- the GameServer never re-checks credentials itself.
/// </summary>
/// <remarks>
///     A repeat of this request on an already-registered connection is a protocol violation per the translated
///     contract, but never reaches <see cref="IZoneHandshakeService" /> at all: <c>ZoneHandshakeRequest.AllowedStates</c>
///     is Connected-only, so <c>SessionStateGate</c> already drops any replay before dispatch (silent, same as
///     every other state violation) -- unlike legacy, which has no such wire-level guard for this packet.
///     This handler's own "Zone handshake accepted..." Info line below is, by construction, logged right after
///     <c>ZoneHandshakeService.ConsumeTicketAsync</c>'s final action stamps this connection's registration
///     timestamp into <c>TribeQuotaRegistry</c> -- which starts the clock for
///     <c>TempRegistrationIdleSweep</c>'s three-minute idle-timeout disconnect (see that class's own remarks).
///     If this line is the last thing logged for a session before it disconnects roughly three minutes later
///     with nothing else in between, check for that sweep's own "TEMP_REGISTER_SEND idle timeout" log line --
///     the client never followed up with EnterWorldRequest/ZoneReadyRequest -- before suspecting a crashed
///     handler.
/// </remarks>
public sealed class ZoneHandshakeHandler(
    IZoneHandshakeService service,
    SessionRegistry registry,
    ILogger<ZoneHandshakeHandler>? logger = null) : IAsyncPacketHandler<ZoneHandshakeRequest>
{
    public async ValueTask HandleAsync(ZoneHandshakeRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: ZoneHandshakeRequest (op11) received, declared tribe {DeclaredTribe}",
            session.SessionId, packet.Tribe);

        var result = await service.ConsumeTicketAsync(packet.Id, packet.Tribe, zoneSession, cancellationToken);

        switch (result.Outcome)
        {
            case ZoneHandshakeOutcome.QuotaFull:
                // Explicit, retry-able Result=1: mirrors legacy's own quota-full/server-state-gate branches,
                // the one failure class on this packet legacy answers with a response instead of a silent
                // disconnect (Server/ts25zone/S04_MyWork02.cpp:630-635, :678-683).
                logger?.LogWarning("Zone handshake rejected for session {SessionId}: {Outcome}", session.SessionId,
                    result.Outcome);
                session.Send(new ZoneHandshakeResponse { Result = 1 });
                return;
            case ZoneHandshakeOutcome.Rejected:
                // Absent/expired/wrong-shard ticket -- the closest structural analog to
                // RegisterUserForZone_00's own failure returns (Server/ts25playuser/S07_MyGame01.cpp:1049-1060),
                // which legacy answers with a silent Quit() and zero response bytes
                // (Server/ts25zone/S04_MyWork02.cpp:728-733), never the explicit response it reserves for the
                // quota-full/server-state-gate class above. Previously grouped with QuotaFull under the same
                // explicit Result=1 (see ZoneHandshakeOutcome.Rejected's own remarks for the resolved
                // product-decision boundary); now given the same silent-disconnect posture as
                // SessionSuperseded/ProtocolViolation below.
                logger?.LogWarning("Zone handshake rejected for session {SessionId}: {Outcome}", session.SessionId,
                    result.Outcome);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case ZoneHandshakeOutcome.SessionSuperseded:
                // A newer login already claimed this account (runtime.AccountSessions moved on since the ticket was
                // minted) -- treated like the login-side duplicate-kick silent drop: no response packet, just drop.
                logger?.LogWarning(
                    "Zone handshake aborted for session {SessionId}: superseded by a newer login for account {AccountId}",
                    session.SessionId, result.AccountId);
                zoneSession.Abort(DisconnectReason.Evicted);
                return;
            case ZoneHandshakeOutcome.ProtocolViolation:
                // Invalid account/session index, or a declared tribe outside this shard's tribe-quota group's
                // valid range -- no response, same silent-drop posture CreateAvatarHandler uses for malformed
                // wire-level input.
                logger?.LogWarning("Zone handshake aborted for session {SessionId}: protocol violation",
                    session.SessionId);
                zoneSession.Abort(DisconnectReason.Malformed);
                return;
        }

        zoneSession.MarkTicketConsumed(result.AccountId, result.CharacterId, result.SessionToken,
            result.AccountGrade);
        registry.AssociateAccount(session.SessionId, result.AccountId);
        session.Send(new ZoneHandshakeResponse { Result = 0 });

        logger?.LogInformation(
            "Zone handshake accepted for account {AccountId} character {CharacterId} (session {SessionId})",
            result.AccountId, result.CharacterId, session.SessionId);
    }
}

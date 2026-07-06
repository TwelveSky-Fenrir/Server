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
        var result = await service.ConsumeTicketAsync(packet.Id, packet.Tribe, zoneSession, cancellationToken);

        switch (result.Outcome)
        {
            case ZoneHandshakeOutcome.Rejected:
            case ZoneHandshakeOutcome.QuotaFull:
                // Same generic Result=1 for every rejection reachable through this path (ticket refusal or a
                // full tribe-population quota) so the client can't distinguish which one occurred; either way
                // the connection stays open and the client can retry.
                logger?.LogWarning("Zone handshake rejected for session {SessionId}: {Outcome}", session.SessionId,
                    result.Outcome);
                session.Send(new ZoneHandshakeResponse { Result = 1 });
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

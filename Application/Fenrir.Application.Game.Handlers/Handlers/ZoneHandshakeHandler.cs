using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op11, first packet after ZC_CONNECT_OK_RECV. Consumes the single-use session ticket the LoginServer minted
///     for this AccountId (ADR-0005) -- the GameServer never re-checks credentials itself.
/// </summary>
public sealed class ZoneHandshakeHandler(IZoneHandshakeService service, SessionRegistry registry)
    : IAsyncPacketHandler<ZoneHandshakeRequest>
{
    public async ValueTask HandleAsync(ZoneHandshakeRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var result = await service.ConsumeTicketAsync(packet.Id, cancellationToken);

        switch (result.Outcome)
        {
            case ZoneHandshakeOutcome.Rejected:
                // Refuse absent/expired/wrong-shard tickets identically (Result=1) so we don't leak which failure occurred.
                session.Send(new ZoneHandshakeResponse { Result = 1 });
                return;
            case ZoneHandshakeOutcome.SessionSuperseded:
                // A newer login already claimed this account (runtime.AccountSessions moved on since the ticket was
                // minted) -- treated like the login-side duplicate-kick silent drop: no response packet, just drop.
                ((ZoneClientSession)session).Abort(DisconnectReason.Evicted);
                return;
        }

        ((ZoneClientSession)session).MarkTicketConsumed(result.AccountId, result.CharacterId, result.SessionToken);
        registry.AssociateAccount(session.SessionId, result.AccountId);
        session.Send(new ZoneHandshakeResponse { Result = 0 });
    }
}

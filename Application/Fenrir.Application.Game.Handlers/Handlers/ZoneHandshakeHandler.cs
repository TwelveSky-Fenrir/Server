using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op11, first packet after ZC_CONNECT_OK_RECV. Consumes the single-use session ticket the LoginServer minted
///     for this AccountId (ADR-0005) -- the GameServer never re-checks credentials itself.
/// </summary>
public sealed class ZoneHandshakeHandler(IZoneHandshakeService service) : IAsyncPacketHandler<ZoneHandshakeRequest>
{
    public async ValueTask HandleAsync(ZoneHandshakeRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var result = await service.ConsumeTicketAsync(packet.Id, cancellationToken);

        // Refuse absent/expired/wrong-shard tickets identically (Result=1) so we don't leak which failure occurred.
        if (result.Outcome == ZoneHandshakeOutcome.Rejected)
        {
            session.Send(new ZoneHandshakeResponse { Result = 1 });
            return;
        }

        ((ZoneClientSession)session).MarkTicketConsumed(result.AccountId, result.CharacterId);
        session.Send(new ZoneHandshakeResponse { Result = 0 });
    }
}

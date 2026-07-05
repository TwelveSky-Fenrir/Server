using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Runtime;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op11, first packet after ZC_CONNECT_OK_RECV. Consumes the single-use session ticket the LoginServer minted
///     for this AccountId (ADR-0005) -- the GameServer never re-checks credentials itself.
/// </summary>
public sealed class ZoneHandshakeHandler(ISessionTicketRepository tickets, IOptions<GameServerOptions> options)
    : IAsyncPacketHandler<ZoneHandshakeRequest>
{
    public async ValueTask HandleAsync(ZoneHandshakeRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        if (!ObfuscatedUidCodec.TryDecodeAccountId(packet.Id, out var accountId))
        {
            session.Send(new ZoneHandshakeResponse { Result = 1 });
            return;
        }

        var consumed = await tickets.ConsumeAsync(accountId, cancellationToken);

        // Refuse absent/expired/wrong-shard tickets identically (Result=1) so we don't leak which failure occurred.
        if (consumed is null || consumed.ShardId != options.Value.ShardId)
        {
            session.Send(new ZoneHandshakeResponse { Result = 1 });
            return;
        }

        ((ZoneClientSession)session).MarkTicketConsumed(accountId, consumed.CharacterId);
        session.Send(new ZoneHandshakeResponse { Result = 0 });
    }
}

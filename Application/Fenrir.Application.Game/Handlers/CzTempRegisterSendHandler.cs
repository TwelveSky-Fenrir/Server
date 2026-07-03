using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Runtime;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op11, the first packet the legacy client sends after ZC_CONNECT_OK_RECV (wire contract §5.2). Performs the
///     ticket→session handover (ADR-0005): the LoginServer minted a single-use session ticket for this AccountId
///     immediately after authenticating the player and handed the account-shard pair to the client via
///     CL_DEMAND_ZONE_SERVER_INFO_SEND. The GameServer never re-checks credentials -- consuming a live ticket for
///     the AccountId embedded in tID (once de-obfuscated, see <see cref="LegacyUidCodec" />) IS the proof of
///     identity. A missing/expired/already-consumed ticket, or one minted for a different shard, is refused
///     identically (Result=1) to avoid leaking which failure mode occurred to an unauthenticated peer.
/// </summary>
public sealed class CzTempRegisterSendHandler(SessionTicketRepository tickets, IOptions<GameServerOptions> options)
    : IAsyncPacketHandler<CzTempRegisterSend>
{
    public async ValueTask HandleAsync(CzTempRegisterSend packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        if (!LegacyUidCodec.TryDecodeAccountId(packet.Id, out var accountId))
        {
            session.Send(new ZcTempRegisterRecv { Result = 1 });
            return;
        }

        var consumed = await tickets.ConsumeAsync(accountId, cancellationToken);

        // Ticket absent/expired/already consumed (ADR-0005, usage unique), or minted for another shard
        // (defense in depth -- M1 has a single shard, so this branch should never trigger in practice).
        if (consumed is null || consumed.ShardId != options.Value.ShardId)
        {
            session.Send(new ZcTempRegisterRecv { Result = 1 });
            return;
        }

        ((ZoneClientSession)session).MarkTicketConsumed(accountId, consumed.CharacterId);
        session.Send(new ZcTempRegisterRecv { Result = 0 });
    }
}

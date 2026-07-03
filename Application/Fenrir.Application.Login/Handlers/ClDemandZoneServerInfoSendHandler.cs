using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Characters;
using Fenrir.Data.Runtime;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     Op22 CL_DEMAND_ZONE_SERVER_INFO_SEND: the login→zone handover (ADR-0005). Resolves the requested
///     character, picks a shard, mints a single-use session ticket, and redirects the client to it — the
///     last packet this login session ever sends (wire contract §4.8: "aucun tID, aucun ticket" on the wire
///     itself, the handover identity lives server-side in the ticket row, keyed on AccountId = uUserIdx).
/// </summary>
public sealed class ClDemandZoneServerInfoSendHandler(
    CharacterRepository characters,
    GameServerDirectoryRepository directory,
    SessionTicketRepository tickets,
    IOptions<LoginServerOptions> options) : IAsyncPacketHandler<ClDemandZoneServerInfoSend>
{
    public async ValueTask HandleAsync(ClDemandZoneServerInfoSend packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;

        // AllowedStates on ClDemandZoneServerInfoSend already gates this to Authenticated/CharSelect, both reachable only past MarkAuthenticated.
        var accountId = loginSession.AccountId!.Value;

        var summaries = await characters.GetByAccountAsync(accountId, cancellationToken);
        var summary = summaries.FirstOrDefault(c => c.Slot == (byte)packet.AvatarPost);
        if (summary is null)
        {
            // Empty or out-of-range slot: Result=1 ("zone fermee") is reused for lack of a more specific documented code (wire contract §4.8).
            session.Send(new LcDemandZoneServerInfoRecv { Result = 1, Ip = "", Port = 0, Zone = 0 });
            return;
        }

        var character = await characters.GetForWorldEntryAsync(summary.CharacterId, cancellationToken);
        if (character is null)
        {
            // Character vanished between the two reads above (e.g. a concurrent CL_DELETE_AVATAR_SEND on another
            // connection for the same account, or a back-office deletion) -- same "zone fermee" fallback as the
            // other two failure branches, and crucially BEFORE any ticket is minted or MarkHandoverIssued() runs.
            session.Send(new LcDemandZoneServerInfoRecv { Result = 1, Ip = "", Port = 0, Zone = 0 });
            return;
        }

        // M1 ships exactly one GameServer/one spawn map (architecture reference: "1 seule map de spawn suffit"),
        // so there is no load-balancing policy to build for this milestone -- FirstOrDefault *is* the policy.
        var shards = await directory.GetDirectoryAsync(cancellationToken);
        var shard = shards.FirstOrDefault();
        if (shard is null)
        {
            // No shard has a live heartbeat row: same "zone fermee" code, nothing more specific documented for this case either.
            session.Send(new LcDemandZoneServerInfoRecv { Result = 1, Ip = "", Port = 0, Zone = 0 });
            return;
        }

        // Single-use, short-TTL ticket keyed on AccountId (ADR-0005): the zone consumes it to prove the handover
        // without the wire ever carrying a tID/ticket (wire contract §4.8's "Aucun tID, aucun ticket" note).
        await tickets.CreateAsync(accountId, summary.CharacterId, shard.ShardId, options.Value.TicketTtlSeconds,
            cancellationToken);

        loginSession.MarkHandoverIssued();

        // tZone = the map the character resumes on (wire contract §4.8: "= aLogoutInfo[0]"), i.e. the persisted
        // MapId -- NOT a shard/infrastructure identifier. AvatarInfoFactory writes that same MapId into
        // LogoutInfo[0] for LC_CREATE_AVATAR_RECV, so this is the identical value taking the op22 path instead.
        session.Send(new LcDemandZoneServerInfoRecv
            { Result = 0, Ip = shard.Host, Port = shard.Port, Zone = character.MapId });
    }
}

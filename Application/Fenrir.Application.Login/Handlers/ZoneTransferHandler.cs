using System.Collections.Immutable;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Data.Admin;
using Fenrir.Data.Characters;
using Fenrir.Data.Runtime;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op22 CL_DEMAND_ZONE_SERVER_INFO_SEND — login-to-zone handover; the handover identity lives server-side in the
///     ticket row, never on the wire.
/// </summary>
public sealed class ZoneTransferHandler(
    ICharacterRepository characters,
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
    ISessionTicketRepository tickets,
    IOptions<LoginServerOptions> options,
    ILogger<ZoneTransferHandler> logger) : IAsyncPacketHandler<ZoneTransferRequest>
{
    public async ValueTask HandleAsync(ZoneTransferRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        var summaries = await characters.GetByAccountAsync(accountId, cancellationToken);
        var summary = summaries.FirstOrDefault(c => c.Slot == (byte)packet.AvatarPost);
        if (summary is null)
        {
            // Result=1 ("zone fermee") reused for lack of a more specific documented code (wire contract §4.8).
            session.Send(new ZoneTransferResponse { Result = 1, Ip = "", Port = 0, Zone = 0 });
            return;
        }

        var character = await characters.GetForWorldEntryAsync(summary.CharacterId, cancellationToken);
        if (character is null)
        {
            // Character vanished between the two reads above (concurrent delete elsewhere); bail before minting a ticket.
            session.Send(new ZoneTransferResponse { Result = 1, Ip = "", Port = 0, Zone = 0 });
            return;
        }

        var shards = await directory.GetDirectoryAsync(cancellationToken);
        var shard = await ResolveShardForMapAsync(shards, character.MapId, character.CharacterId, cancellationToken);
        if (shard is null)
        {
            session.Send(new ZoneTransferResponse { Result = 1, Ip = "", Port = 0, Zone = 0 });
            return;
        }

        await tickets.CreateAsync(accountId, summary.CharacterId, shard.ShardId, options.Value.TicketTtlSeconds,
            cancellationToken);

        loginSession.MarkHandoverIssued();

        // Zone = the persisted MapId the character resumes on (same value AvatarInfoFactory writes to LogoutInfo[0]).
        session.Send(new ZoneTransferResponse
            { Result = 0, Ip = shard.Host, Port = shard.Port, Zone = character.MapId });
    }

    private async ValueTask<ShardDirectoryEntryDto?> ResolveShardForMapAsync(
        ImmutableArray<ShardDirectoryEntryDto> shards, short mapId, int characterId, CancellationToken ct)
    {
        foreach (var candidate in shards)
        {
            var hostedMaps = await shardMapAssignments.GetHostedMapsAsync(candidate.ShardId, ct);
            if (hostedMaps.Contains(mapId))
                return candidate;
        }

        logger.LogWarning(
            "No shard in admin.ShardMapAssignments hosts MapId {MapId} for character {CharacterId}; falling back to first live shard",
            mapId, characterId);
        return shards.FirstOrDefault();
    }
}

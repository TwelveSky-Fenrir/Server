using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class PartyResyncRelayHandler(
    ZoneRegistry zones,
    PartyRegistry parties,
    Lazy<IPartyResyncRelayQueue> relay,
    IOptions<GameServerOptions> options,
    ILogger<PartyResyncRelayHandler> logger) : IPartyResyncRelayHandler
{
    public ValueTask HandleAsync(PartyResyncRelayDto row, CancellationToken ct)
    {
        switch ((PartyResyncRelaySort)row.Sort)
        {
            case PartyResyncRelaySort.Request:
                HandleRequest(row);
                return ValueTask.CompletedTask;

            case PartyResyncRelaySort.PartyInfoReply:
                DeliverIfLocal(row.SourceCharacterId,
                    new PartyRosterResponse
                    {
                        Sort = 3,
                        AvatarName01 = row.PartyName,
                        AvatarName02 = "",
                        AvatarName03 = "",
                        AvatarName04 = "",
                        AvatarName05 = ""
                    },
                    row.RelayId, "party-info-reply");
                return ValueTask.CompletedTask;

            case PartyResyncRelaySort.PartyBreak:
                DeliverIfLocal(row.SourceCharacterId, new PartyDisbandResponse { Sort = 1, AvatarName = "" },
                    row.RelayId, "party-break");
                return ValueTask.CompletedTask;

            case PartyResyncRelaySort.LeaveNotice:
                DeliverIfLocal(row.SourceCharacterId, new PartyLeaveResponse { AvatarName = row.AvatarName },
                    row.RelayId, "leave-notice");
                return ValueTask.CompletedTask;

            case PartyResyncRelaySort.KickNotice:
                DeliverIfLocal(row.SourceCharacterId, new PartyKickResponse { AvatarName = row.AvatarName },
                    row.RelayId, "kick-notice");
                return ValueTask.CompletedTask;

            case PartyResyncRelaySort.DisbandNotice:
                DeliverIfLocal(row.SourceCharacterId, new PartyDisbandResponse { Sort = 1, AvatarName = "" },
                    row.RelayId, "disband-notice");
                return ValueTask.CompletedTask;

            default:
                logger.LogWarning("Relayed party-resync row {RelayId} has unrecognized Sort {Sort}; dropped",
                    row.RelayId, row.Sort);
                return ValueTask.CompletedTask;
        }
    }

    private void HandleRequest(PartyResyncRelayDto row)
    {
        if (!parties.IsInParty(row.SourceCharacterId))
        {
            logger.LogDebug(
                "Relayed party-resync request {RelayId} for character {SourceCharacterId} has no matching " +
                "party on shard {ShardId}",
                row.RelayId, row.SourceCharacterId, options.Value.ShardId);
            return;
        }

        var roster = parties.GetMembers(row.SourceCharacterId);
        if (roster.Count == 0)
        {
            logger.LogDebug(
                "Relayed party-resync request {RelayId} for character {SourceCharacterId} raced a disband on " +
                "shard {ShardId} between the membership check and the roster fetch",
                row.RelayId, row.SourceCharacterId, options.Value.ShardId);
            return;
        }

        var leaderId = roster[0];
        var leaderName = zones.TryGetPlayer(leaderId, out var leader) ? leader.Name : "";

        relay.Value.Enqueue(new PartyResyncRelayEntry(
            (byte)PartyResyncRelaySort.PartyInfoReply,
            options.Value.ShardId,
            row.SourceCharacterId,
            leaderName,
            row.AvatarName));

        logger.LogDebug(
            "Relayed party-resync request {RelayId} for character {SourceCharacterId} confirmed on shard " +
            "{ShardId} ({MemberCount} members); leader {LeaderName} republished",
            row.RelayId, row.SourceCharacterId, options.Value.ShardId, roster.Count, leaderName);
    }

    private void DeliverIfLocal<TPacket>(int characterId, in TPacket packet, long relayId, string kind)
        where TPacket : struct, IOutgoingPacket
    {
        if (!zones.TryGetPlayer(characterId, out var recipient))
            return;

        recipient.Session.Send(packet);
        logger.LogDebug(
            "Applied relayed party {Kind} {RelayId} to locally-connected character {CharacterId} on shard {ShardId}",
            kind, relayId, characterId, options.Value.ShardId);
    }
}

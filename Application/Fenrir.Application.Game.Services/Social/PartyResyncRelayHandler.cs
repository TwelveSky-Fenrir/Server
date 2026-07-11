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
                logger.LogDebug(
                    "Relayed party-resync reply {RelayId} confirms character {SourceCharacterId} is still " +
                    "partied (party {PartyName}); no local UI change needed on shard {ShardId}",
                    row.RelayId, row.SourceCharacterId, row.PartyName, options.Value.ShardId);
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
        if (!zones.TryGetPlayerByName(row.PartyName, out var leaderCandidate) ||
            !parties.IsLeader(leaderCandidate.CharacterId))
        {
            logger.LogDebug(
                "Relayed party-resync request {RelayId} for party {PartyName} (subject {SourceCharacterId}) " +
                "is not hosted on shard {ShardId}",
                row.RelayId, row.PartyName, row.SourceCharacterId, options.Value.ShardId);
            return;
        }

        var roster = parties.GetMembers(leaderCandidate.CharacterId);
        if (roster.Count == 0)
        {
            logger.LogDebug(
                "Relayed party-resync request {RelayId} for party {PartyName} raced a disband on shard " +
                "{ShardId} between the leader check and the roster fetch; treated as not hosted",
                row.RelayId, row.PartyName, options.Value.ShardId);
            return;
        }

        relay.Value.Enqueue(new PartyResyncRelayEntry(
            (byte)PartyResyncRelaySort.PartyInfoReply,
            options.Value.ShardId,
            row.SourceCharacterId,
            row.PartyName,
            row.AvatarName));

        logger.LogDebug(
            "Relayed party-resync request {RelayId} for party {PartyName} confirmed on shard {ShardId} " +
            "({MemberCount} members); result republished for subject {SourceCharacterId}",
            row.RelayId, row.PartyName, options.Value.ShardId, roster.Count, row.SourceCharacterId);
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

using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
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
                HandlePartyInfoReply(row);
                return ValueTask.CompletedTask;

            case PartyResyncRelaySort.PartyBreak:
                parties.DisbandFor(row.SourceCharacterId);
                DeliverIfLocal(row.SourceCharacterId, new PartyDisbandResponse { Sort = 1, AvatarName = "" },
                    row.RelayId, "party-break");
                return ValueTask.CompletedTask;

            case PartyResyncRelaySort.LeaveNotice:
                parties.TryRemoveMemberByName(row.SourceCharacterId, row.AvatarName, out _);
                DeliverIfLocal(row.SourceCharacterId, new PartyLeaveResponse { AvatarName = row.AvatarName },
                    row.RelayId, "leave-notice");
                return ValueTask.CompletedTask;

            case PartyResyncRelaySort.KickNotice:
                parties.TryRemoveMemberByName(row.SourceCharacterId, row.AvatarName, out _);
                DeliverIfLocal(row.SourceCharacterId, new PartyKickResponse { AvatarName = row.AvatarName },
                    row.RelayId, "kick-notice");
                return ValueTask.CompletedTask;

            case PartyResyncRelaySort.DisbandNotice:
                parties.DisbandFor(row.SourceCharacterId);
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
        if (row.CorrelationId == Guid.Empty || row.RequestCorrelationId != Guid.Empty ||
            row.SourceShardId == options.Value.ShardId ||
            row.RecipientCharacterId != row.SourceCharacterId || string.IsNullOrWhiteSpace(row.AvatarName) ||
            !string.Equals(row.PartyName, row.AvatarName, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Relayed party-resync request {RelayId} rejected: its recipient, source, shard, avatar, or correlation binding was invalid",
                row.RelayId);
            return;
        }

        if (!parties.IsInParty(row.SourceCharacterId))
        {
            logger.LogDebug(
                "Relayed party-resync request {RelayId} for character {SourceCharacterId} has no matching " +
                "party on shard {ShardId}",
                row.RelayId, row.SourceCharacterId, options.Value.ShardId);
            return;
        }

        var roster = parties.GetRoster(row.SourceCharacterId);
        if (roster.Count == 0)
        {
            logger.LogDebug(
                "Relayed party-resync request {RelayId} for character {SourceCharacterId} raced a disband on " +
                "shard {ShardId} between the membership check and the roster fetch",
                row.RelayId, row.SourceCharacterId, options.Value.ShardId);
            return;
        }

        PartyMember requester = default;
        var requesterFound = false;
        foreach (var member in roster)
            if (member.CharacterId == row.SourceCharacterId)
            {
                requester = member;
                requesterFound = true;
                break;
            }

        if (!requesterFound || !string.Equals(requester.Name, row.AvatarName, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Relayed party-resync request {RelayId} rejected: source character {SourceCharacterId} is not bound to its claimed avatar",
                row.RelayId, row.SourceCharacterId);
            return;
        }

        var leader = roster[0];

        relay.Value.Enqueue(new PartyResyncRelayEntry(
            (byte)PartyResyncRelaySort.PartyInfoReply,
            options.Value.ShardId,
            leader.CharacterId,
            leader.Name,
            row.AvatarName)
        {
            RecipientCharacterId = row.SourceCharacterId,
            RequestCorrelationId = row.CorrelationId,
            MemberId1 = MemberIdAt(roster, 0),
            MemberName1 = MemberNameAt(roster, 0),
            MemberId2 = MemberIdAt(roster, 1),
            MemberName2 = MemberNameAt(roster, 1),
            MemberId3 = MemberIdAt(roster, 2),
            MemberName3 = MemberNameAt(roster, 2),
            MemberId4 = MemberIdAt(roster, 3),
            MemberName4 = MemberNameAt(roster, 3),
            MemberId5 = MemberIdAt(roster, 4),
            MemberName5 = MemberNameAt(roster, 4)
        });

        logger.LogDebug(
            "Relayed party-resync request {RelayId} for character {SourceCharacterId} confirmed on shard " +
            "{ShardId} ({MemberCount} members); leader {LeaderName} republished",
            row.RelayId, row.SourceCharacterId, options.Value.ShardId, roster.Count, leader.Name);
    }

    private void HandlePartyInfoReply(PartyResyncRelayDto row)
    {
        if (row.CorrelationId == Guid.Empty || row.RequestCorrelationId == Guid.Empty ||
            row.SourceShardId == options.Value.ShardId ||
            row.RecipientCharacterId <= 0 || row.SourceCharacterId <= 0)
        {
            logger.LogWarning(
                "Relayed party-resync reply {RelayId} rejected: its recipient, source, shard, or correlation binding was invalid",
                row.RelayId);
            return;
        }

        if (!zones.TryGetPlayer(row.RecipientCharacterId, out var recipient))
            return;

        if (!string.Equals(row.AvatarName, recipient.Name, StringComparison.OrdinalIgnoreCase) ||
            !TryReadRoster(row, out var roster))
        {
            logger.LogWarning(
                "Relayed party-resync reply {RelayId} rejected: its roster did not authenticate the addressed recipient or source",
                row.RelayId);
            return;
        }

        if (row.SourceCharacterId != roster[0].CharacterId ||
            !string.Equals(row.PartyName, roster[0].Name, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Relayed party-resync reply {RelayId} rejected: its party leader does not bind its authenticated source",
                row.RelayId);
            return;
        }

        if (!parties.TryApplyResyncRoster(row.RecipientCharacterId, recipient.Name, row.RequestCorrelationId,
                roster))
        {
            logger.LogInformation(
                "Relayed party-resync reply {RelayId} for character {RecipientCharacterId} was not attached to a pending local request or would replace local membership",
                row.RelayId, row.RecipientCharacterId);
            return;
        }

        var packet = new PartyRosterResponse
        {
            Sort = 3,
            AvatarName01 = MemberNameAt(roster, 0),
            AvatarName02 = MemberNameAt(roster, 1),
            AvatarName03 = MemberNameAt(roster, 2),
            AvatarName04 = MemberNameAt(roster, 3),
            AvatarName05 = MemberNameAt(roster, 4)
        };

        foreach (var member in roster)
            DeliverIfLocal(member.CharacterId, packet, row.RelayId, "party-info-reply");

        logger.LogInformation(
            "Relayed party-resync reply {RelayId} re-registered a {MemberCount}-member party led by " +
            "{LeaderName} on shard {ShardId} for character {RecipientCharacterId}",
            row.RelayId, roster.Count, roster[0].Name, options.Value.ShardId, row.RecipientCharacterId);
    }

    private static bool TryReadRoster(PartyResyncRelayDto row, out List<PartyMember> roster)
    {
        roster = new List<PartyMember>(PartyRegistry.MaxMembers);

        if (!Append(roster, row.MemberId1, row.MemberName1) ||
            !Append(roster, row.MemberId2, row.MemberName2) ||
            !Append(roster, row.MemberId3, row.MemberName3) ||
            !Append(roster, row.MemberId4, row.MemberName4) ||
            !Append(roster, row.MemberId5, row.MemberName5) ||
            roster.Count is < 2 or > PartyRegistry.MaxMembers)
            return false;

        var memberIds = new HashSet<int>();
        var memberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in roster)
            if (!memberIds.Add(member.CharacterId) || !memberNames.Add(member.Name))
                return false;

        return true;

        static bool Append(List<PartyMember> target, int characterId, string name)
        {
            if (characterId == 0)
                return string.IsNullOrEmpty(name);

            if (characterId < 0 || string.IsNullOrWhiteSpace(name))
                return false;

            target.Add(new PartyMember(characterId, name));
            return true;
        }
    }

    private static int MemberIdAt(IReadOnlyList<PartyMember> roster, int index)
    {
        return index < roster.Count ? roster[index].CharacterId : 0;
    }

    private static string MemberNameAt(IReadOnlyList<PartyMember> roster, int index)
    {
        return index < roster.Count ? roster[index].Name : "";
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

using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class PartyCrossShardRelayHandler(
    ZoneRegistry zones,
    PartyRegistry parties,
    Lazy<ISocialCrossShardRelayQueue> crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<PartyCrossShardRelayHandler> logger) : ISocialCrossShardRelayHandler
{
    public SocialCrossShardRelayKind Kind => SocialCrossShardRelayKind.Party;

        public ValueTask HandleAskAsync(SocialCrossShardRelayDto ask, CancellationToken ct)
    {
        if (!zones.TryGetPlayer(ask.TargetCharacterId, out var target))
        {
            PublishDecline(ask, 4);
            return ValueTask.CompletedTask;
        }

        if (!parties.TryRegisterCrossShardInbound(target.CharacterId,
                new CrossShardInboundAsk(ask.RelayId, ask.SourceShardId, ask.SourceCharacterId,
                    ask.SourceAvatarName)))
        {
            PublishDecline(ask, 5);
            return ValueTask.CompletedTask;
        }

        target.Session.Send(new PartyInviteResponse { AvatarName = ask.SourceAvatarName });
        logger.LogDebug(
            "Cross-shard party invite delivered: character {TargetId} <- inviter {SourceCharacterId} on shard {SourceShardId}",
            target.CharacterId, ask.SourceCharacterId, ask.SourceShardId);
        return ValueTask.CompletedTask;
    }

        public ValueTask HandleAnswerAsync(SocialCrossShardRelayDto answer, CancellationToken ct)
    {
        if (!parties.TryConsumeCrossShardOutbound(answer.TargetCharacterId, out _))
        {
            logger.LogInformation(
                "Cross-shard party answer for inviter {InviterId} has no matching pending invite -- inviter already cancelled/disconnected, or a stale/duplicate Answer",
                answer.TargetCharacterId);
            return ValueTask.CompletedTask;
        }

        var inviterId = answer.TargetCharacterId;
        var accepted = answer.Accepted == true;

        if (zones.TryGetPlayer(inviterId, out var inviter))
            inviter.Session.Send(new PartyAnswerResponse { Answer = accepted ? 0 : answer.ReasonCode ?? 1 });

        if (!accepted)
        {
            logger.LogDebug(
                "Cross-shard party invite declined: inviter {InviterId} <- invitee {InviteeId} on shard {InviteeShardId}",
                inviterId, answer.SourceCharacterId, answer.SourceShardId);
            return ValueTask.CompletedTask;
        }

        var joinOutcome = parties.TryCompleteCrossShardAnswer(inviterId, answer.SourceCharacterId, out var members);
        if (joinOutcome == PartyJoinOutcome.PartyWasFull)
        {
            logger.LogDebug(
                "Cross-shard party invite accepted but not joined: inviter {InviterId}'s party was already full when invitee {InviteeId} answered",
                inviterId, answer.SourceCharacterId);
            return ValueTask.CompletedTask;
        }

        logger.LogInformation(
            "Cross-shard party {JoinOutcome}: invitee {InviteeId} (on shard {InviteeShardId}) joined inviter {InviterId}'s party ({MemberCount} members)",
            joinOutcome, answer.SourceCharacterId, answer.SourceShardId, inviterId, members.Count);

        var joinNotice = new PartyMemberJoinedResponse { AvatarName = answer.SourceAvatarName };
        var roster = BuildRosterLocalOnly(joinOutcome == PartyJoinOutcome.Created ? 1 : 2, members);

        foreach (var memberId in members)
            if (zones.TryGetPlayer(memberId, out var member))
            {
                member.Session.Send(joinNotice);
                member.Session.Send(roster);
            }

        return ValueTask.CompletedTask;
    }

    private void PublishDecline(SocialCrossShardRelayDto ask, byte reasonCode)
    {
        crossShardRelay.Value.Enqueue(new SocialCrossShardRelayEntry(
            SocialCrossShardRelayKind.Party,
            SocialCrossShardRelayMessageType.Answer,
            false,
            reasonCode,
            options.Value.ShardId,
            ask.TargetCharacterId,
            "",
            ask.SourceShardId,
            ask.SourceCharacterId,
            ask.RelayId));
    }

        private PartyRosterResponse BuildRosterLocalOnly(int sort, IReadOnlyList<int> memberIds)
    {
        Span<string> names = ["", "", "", "", ""];
        for (var i = 0; i < memberIds.Count && i < 5; i++)
            if (zones.TryGetPlayer(memberIds[i], out var member))
                names[i] = member.Name;

        return new PartyRosterResponse
        {
            Sort = sort,
            AvatarName01 = names[0],
            AvatarName02 = names[1],
            AvatarName03 = names[2],
            AvatarName04 = names[3],
            AvatarName05 = names[4]
        };
    }
}

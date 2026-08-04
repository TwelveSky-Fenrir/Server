using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class PartyCrossShardRelayHandler(
    ZoneRegistry zones,
    PartyRegistry parties,
    DuelRegistry duels,
    TradeRegistry trades,
    FriendRegistry friends,
    MentorRegistry mentors,
    GuildInviteRegistry guildInvites,
    Lazy<ISocialCrossShardRelayQueue> crossShardRelay,
    Lazy<IPartyResyncRelayQueue> partyResyncRelay,
    IOptions<GameServerOptions> options,
    ILogger<PartyCrossShardRelayHandler> logger) : ISocialCrossShardRelayHandler
{
    private const byte LevelGapDisconnectReason = 7;

    public SocialCrossShardRelayKind Kind => SocialCrossShardRelayKind.Party;

    public ValueTask HandleAskAsync(SocialCrossShardRelayDto ask, CancellationToken ct)
    {
        if (!zones.TryGetPlayer(ask.TargetCharacterId, out var target))
        {
            PublishDecline(ask, 4);
            return ValueTask.CompletedTask;
        }

        if (ask.SourceCombinedLevel is not { } inviterCombinedLevel || inviterCombinedLevel == 0 ||
            Math.Abs(inviterCombinedLevel - target.CombinedLevel) > PartyRegistry.MaxLevelGap)
        {
            PublishDecline(ask, LevelGapDisconnectReason);
            return ValueTask.CompletedTask;
        }

        if (parties.IsInParty(target.CharacterId))
        {
            PublishDecline(ask, 6);
            return ValueTask.CompletedTask;
        }

        if (CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, guildInvites) ||
            target.IsStunned || target.IsDead || target.IsMovingZone)
        {
            PublishDecline(ask, 5);
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
        if (!parties.TryConsumeCrossShardOutbound(answer.TargetCharacterId, answer.SourceShardId,
                answer.SourceCharacterId, out _))
        {
            logger.LogInformation(
                "Cross-shard party answer for inviter {InviterId} has no matching pending invite from character {InviteeId} on shard {InviteeShardId} -- cancelled, stale, or mismatched Answer",
                answer.TargetCharacterId, answer.SourceCharacterId, answer.SourceShardId);
            return ValueTask.CompletedTask;
        }

        var inviterId = answer.TargetCharacterId;
        var accepted = answer.Accepted == true;

        zones.TryGetPlayer(inviterId, out var inviter);

        if (answer.ReasonCode == LevelGapDisconnectReason)
        {
            if (inviter is not null)
                ((IZoneSession)inviter.Session).Abort(DisconnectReason.Faulted);

            logger.LogWarning(
                "Cross-shard party invite rejected for level gap: inviter {InviterId}, invitee {InviteeId} on shard {InviteeShardId}",
                inviterId, answer.SourceCharacterId, answer.SourceShardId);
            return ValueTask.CompletedTask;
        }

        if (inviter is not null)
            inviter.Session.Send(new PartyAnswerResponse { Answer = accepted ? 0 : answer.ReasonCode ?? 1 });

        if (!accepted)
        {
            logger.LogDebug(
                "Cross-shard party invite declined: inviter {InviterId} <- invitee {InviteeId} on shard {InviteeShardId}",
                inviterId, answer.SourceCharacterId, answer.SourceShardId);
            return ValueTask.CompletedTask;
        }

        if (inviter is null)
        {
            logger.LogInformation(
                "Cross-shard party invite accepted but dropped: inviter {InviterId} left before invitee {InviteeId} answered",
                inviterId, answer.SourceCharacterId);
            return ValueTask.CompletedTask;
        }

        var joinOutcome = parties.TryCompleteCrossShardAnswer(
            new PartyMember(inviterId, inviter.Name),
            new PartyMember(answer.SourceCharacterId, answer.SourceAvatarName),
            out var members);

        if (joinOutcome == PartyJoinOutcome.PartyWasFull)
        {
            logger.LogDebug(
                "Cross-shard party invite accepted but not joined: inviter {InviterId}'s party was already full when invitee {InviteeId} answered",
                inviterId, answer.SourceCharacterId);

            var fullRoster = BuildRoster(2, members);
            foreach (var current in members)
                if (zones.TryGetPlayer(current.CharacterId, out var member))
                    member.Session.Send(fullRoster);

            PublishRosterToJoinerShard(answer.SourceCharacterId, answer.SourceAvatarName, members);

            return ValueTask.CompletedTask;
        }

        logger.LogInformation(
            "Cross-shard party {JoinOutcome}: invitee {InviteeId} (on shard {InviteeShardId}) joined inviter {InviterId}'s party ({MemberCount} members)",
            joinOutcome, answer.SourceCharacterId, answer.SourceShardId, inviterId, members.Count);

        var joinNotice = new PartyMemberJoinedResponse { AvatarName = answer.SourceAvatarName };
        var roster = BuildRoster(joinOutcome == PartyJoinOutcome.Created ? 1 : 2, members);

        foreach (var current in members)
            if (zones.TryGetPlayer(current.CharacterId, out var member))
            {
                member.Session.Send(joinNotice);
                member.Session.Send(roster);
            }

        PublishRostersToRemoteMembers(members);

        return ValueTask.CompletedTask;
    }

    public ValueTask HandleCancelAsync(SocialCrossShardRelayDto cancel, CancellationToken ct)
    {
        if (!zones.TryGetPlayer(cancel.TargetCharacterId, out var invitee) || invitee.IsMovingZone)
            return ValueTask.CompletedTask;

        if (!parties.TryClearCrossShardInbound(invitee.CharacterId, cancel.SourceShardId,
                cancel.SourceCharacterId))
        {
            logger.LogDebug(
                "Cross-shard party cancel ignored for invitee {InviteeId}: no matching pending invite from character {InviterId} on shard {InviterShardId}",
                invitee.CharacterId, cancel.SourceCharacterId, cancel.SourceShardId);
            return ValueTask.CompletedTask;
        }

        invitee.Session.Send(new PartyCancelResponse());
        logger.LogDebug(
            "Cross-shard party invite cancelled for invitee {InviteeId} by character {InviterId} on shard {InviterShardId}",
            invitee.CharacterId, cancel.SourceCharacterId, cancel.SourceShardId);
        return ValueTask.CompletedTask;
    }

    private void PublishRosterToJoinerShard(int joinerCharacterId, string joinerAvatarName,
        IReadOnlyList<PartyMember> members)
    {
        PublishRoster(joinerCharacterId, joinerAvatarName, members);
    }

    private void PublishRostersToRemoteMembers(IReadOnlyList<PartyMember> members)
    {
        foreach (var member in members)
            if (!zones.TryGetPlayer(member.CharacterId, out _))
                PublishRoster(member.CharacterId, member.Name, members);
    }

    private void PublishRoster(int recipientCharacterId, string recipientAvatarName,
        IReadOnlyList<PartyMember> members)
    {
        partyResyncRelay.Value.Enqueue(new PartyResyncRelayEntry(
            (byte)PartyResyncRelaySort.PartyInfoReply,
            options.Value.ShardId,
            recipientCharacterId,
            members[0].Name,
            recipientAvatarName)
        {
            MemberId1 = MemberIdAt(members, 0),
            MemberName1 = MemberNameAt(members, 0),
            MemberId2 = MemberIdAt(members, 1),
            MemberName2 = MemberNameAt(members, 1),
            MemberId3 = MemberIdAt(members, 2),
            MemberName3 = MemberNameAt(members, 2),
            MemberId4 = MemberIdAt(members, 3),
            MemberName4 = MemberNameAt(members, 3),
            MemberId5 = MemberIdAt(members, 4),
            MemberName5 = MemberNameAt(members, 4)
        });
    }

    private static int MemberIdAt(IReadOnlyList<PartyMember> members, int index)
    {
        return index < members.Count ? members[index].CharacterId : 0;
    }

    private static string MemberNameAt(IReadOnlyList<PartyMember> members, int index)
    {
        return index < members.Count ? members[index].Name : "";
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

    private static PartyRosterResponse BuildRoster(int sort, IReadOnlyList<PartyMember> members)
    {
        Span<string> names = ["", "", "", "", ""];
        for (var i = 0; i < members.Count && i < 5; i++)
            names[i] = members[i].Name;

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

using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     WS1.4 target-shard delivery and inviter-shard completion for cross-shard CZ_PARTY_ASK_SEND/
///     CZ_PARTY_ANSWER_SEND negotiations that <see cref="PartyInviteService" /> itself could not complete
///     locally (same-shard <c>ZoneRegistry</c> miss, resolved via <see cref="ICharacterShardLocationRepository" />
///     -- see <see cref="PartyInviteService.InviteAsync" />'s own remarks). Registered as
///     <see cref="ISocialCrossShardRelayHandler" /> for <see cref="SocialCrossShardRelayKind.Party" />;
///     <c>SocialCrossShardRelayHost</c> routes every delivered Ask/Answer row for that Kind here.
/// </summary>
/// <remarks>
///     The actual JOIN can only ever be committed on the INVITER's own shard
///     (<see cref="PartyRegistry.TryCompleteCrossShardAnswer" />), because <see cref="PartyRegistry" /> is a
///     process-wide, not cluster-wide, authority -- a brand-new party's only "home" is whichever process
///     created it, exactly as it already is for a same-shard invite (see <see cref="PartyRegistry" />'s own
///     "CROSS-SHARD SCOPE" remarks). This means the roster/join-notice fan-out below only ever reaches
///     members who happen to be locally present on THIS shard's own <see cref="ZoneRegistry" />; the remote
///     invitee (present only on their own shard) receives no equivalent live confirmation today -- the same
///     already-documented, unaddressed cross-shard party-visibility gap, not something this reference
///     implementation newly introduces or attempts to close.
/// </remarks>
public sealed class PartyCrossShardRelayHandler(
    ZoneRegistry zones,
    PartyRegistry parties,
    Lazy<ISocialCrossShardRelayQueue> crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<PartyCrossShardRelayHandler> logger) : ISocialCrossShardRelayHandler
{
    public SocialCrossShardRelayKind Kind => SocialCrossShardRelayKind.Party;

    /// <summary>Runs on the TARGET's (invitee's) own shard.</summary>
    public ValueTask HandleAskAsync(SocialCrossShardRelayDto ask, CancellationToken ct)
    {
        if (!zones.TryGetPlayer(ask.TargetCharacterId, out var target))
        {
            PublishDecline(ask, reasonCode: 4);
            return ValueTask.CompletedTask;
        }

        if (!parties.TryRegisterCrossShardInbound(target.CharacterId,
                new CrossShardInboundAsk(ask.RelayId, ask.SourceShardId, ask.SourceCharacterId,
                    ask.SourceAvatarName)))
        {
            // TryRegisterCrossShardInbound itself checks already-partied/already-negotiating (see that
            // method's own remarks) -- collapsed to the same generic busy reply either way, matching how
            // the same-shard TryInvite path also collapses "target already partied" into its own distinct
            // code, but that distinction is not reconstructable here without a second local lookup this
            // registry does not expose; busy (5) is the closer of the two same-shard codes for an already
            // occupied target.
            PublishDecline(ask, reasonCode: 5);
            return ValueTask.CompletedTask;
        }

        target.Session.Send(new PartyInviteResponse { AvatarName = ask.SourceAvatarName });
        logger.LogDebug(
            "Cross-shard party invite delivered: character {TargetId} <- inviter {SourceCharacterId} on shard {SourceShardId}",
            target.CharacterId, ask.SourceCharacterId, ask.SourceShardId);
        return ValueTask.CompletedTask;
    }

    /// <summary>Runs on the original INVITER's own shard.</summary>
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

        // Fan out to whichever members happen to be locally present on THIS shard only -- see this type's
        // own remarks for why the remote invitee cannot be reached from here.
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

    /// <summary>
    ///     Duplicated from the internal <c>PartyBroadcast.BuildRoster</c> helper that
    ///     <c>Fenrir.Application.Game.Handlers</c>' own <c>PartyAnswerHandler</c> uses for the same-shard
    ///     path -- <c>*.Services</c> cannot reference <c>*.Handlers</c> (one-way layering, see
    ///     <c>fenrir-clean-architecture-layering</c>), so this is a deliberate small duplication rather than a
    ///     new shared project reference neither layer otherwise needs. 5 name slots, leader first
    ///     (<paramref name="memberIds" />[0]); resolved live since a member could be in any zone this shard hosts.
    /// </summary>
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

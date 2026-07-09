using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     On accept, collapses legacy's separate PARTY_JOIN/PARTY_INFO emissions into one fan-out; a full party
///     (<see cref="PartyJoinOutcome.PartyWasFull" />) is a silent no-op.
/// </summary>
public sealed class PartyAnswerService(
    PartyRegistry parties,
    ZoneRegistry zones,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<PartyAnswerService> logger)
    : IPartyAnswerService
{
    public PartyAnswerResult Answer(int inviteeId, int answer)
    {
        if (answer is not (0 or 1 or 2))
        {
            logger.LogDebug("Party answer rejected: character {InviteeId} sent malformed answer code {Answer}",
                inviteeId, answer);
            return new PartyAnswerResult(PartyAnswerResultKind.NotFound);
        }

        var accepted = answer == 0;

        // WS1.4: if this invitee has a cross-shard invite delivered in (via
        // PartyCrossShardRelayHandler.HandleAskAsync), answer it by publishing an Answer row back to the
        // original inviter's own shard -- transparent to the caller (PartyAnswerHandler), which is unaware
        // whether a given pending invite is same-shard or cross-shard. The actual join can only happen on
        // the inviter's own shard (see PartyRegistry.TryConsumeCrossShardInbound's own remarks), so nothing
        // party-membership-related is mutated here; InviterId=0/empty Members tells the caller there is no
        // local join fan-out to perform.
        if (parties.TryConsumeCrossShardInbound(inviteeId, out var inbound))
        {
            var inviteeName = zones.TryGetPlayer(inviteeId, out var inviteeState) ? inviteeState.Name : "";

            crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
                SocialCrossShardRelayKind.Party,
                SocialCrossShardRelayMessageType.Answer,
                accepted,
                null,
                options.Value.ShardId,
                inviteeId,
                inviteeName,
                inbound.SourceShardId,
                inbound.SourceCharacterId,
                inbound.RelayId));

            logger.LogDebug(
                "Party answer (cross-shard): character {InviteeId} answered {Answer} to inviter {InviterId} on shard {InviterShardId}",
                inviteeId, answer, inbound.SourceCharacterId, inbound.SourceShardId);
            return new PartyAnswerResult(PartyAnswerResultKind.Answered, 0, accepted);
        }

        if (!parties.TryAnswer(inviteeId, accepted, out var inviterId, out var joinOutcome))
        {
            logger.LogDebug("Party answer ignored: character {InviteeId} has no pending invite", inviteeId);
            return new PartyAnswerResult(PartyAnswerResultKind.NotFound);
        }

        if (!accepted)
        {
            logger.LogDebug("Party invite declined: character {InviteeId} declined inviter {InviterId}", inviteeId,
                inviterId);
            return new PartyAnswerResult(PartyAnswerResultKind.Answered, inviterId, false, joinOutcome, []);
        }

        if (joinOutcome == PartyJoinOutcome.PartyWasFull)
        {
            logger.LogDebug(
                "Party invite accepted but not joined: inviter {InviterId}'s party was already full when character {InviteeId} answered",
                inviterId, inviteeId);
            return new PartyAnswerResult(PartyAnswerResultKind.Answered, inviterId, true, joinOutcome, []);
        }

        var members = parties.GetMembers(inviterId);
        logger.LogInformation(
            "Party {JoinOutcome}: character {InviteeId} joined inviter {InviterId}'s party ({MemberCount} members)",
            joinOutcome, inviteeId, inviterId, members.Count);

        return new PartyAnswerResult(PartyAnswerResultKind.Answered, inviterId, true, joinOutcome, members);
    }
}

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

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

        if (!parties.TryPeekPending(inviteeId, out var pendingInviterId, out var isInviter) || isInviter)
        {
            logger.LogDebug("Party answer ignored: character {InviteeId} has no pending invite", inviteeId);
            return new PartyAnswerResult(PartyAnswerResultKind.NotFound);
        }

        var inviterBusyByZoneTransfer =
            !zones.TryGetPlayer(pendingInviterId, out var pendingInviter) || pendingInviter.IsMovingZone;

        if (!parties.TryAnswer(inviteeId, accepted, inviterBusyByZoneTransfer, out var inviterId,
                out var joinOutcome, out var guardBlocked))
        {
            if (guardBlocked)
                logger.LogDebug(
                    "Party answer: character {InviteeId} answered but counterpart (inviter) {InviterId} is unreachable or mid zone-transfer -- no notice sent, negotiation state left as-is",
                    inviteeId, inviterId);
            else
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
            return new PartyAnswerResult(PartyAnswerResultKind.Answered, inviterId, true, joinOutcome,
                parties.GetMembers(inviterId));
        }

        var members = parties.GetMembers(inviterId);
        logger.LogInformation(
            "Party {JoinOutcome}: character {InviteeId} joined inviter {InviterId}'s party ({MemberCount} members)",
            joinOutcome, inviteeId, inviterId, members.Count);

        return new PartyAnswerResult(PartyAnswerResultKind.Answered, inviterId, true, joinOutcome, members);
    }
}

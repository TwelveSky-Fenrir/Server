using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     On accept, collapses legacy's separate PARTY_JOIN/PARTY_INFO emissions into one fan-out; a full party
///     (<see cref="PartyJoinOutcome.PartyWasFull" />) is a silent no-op.
/// </summary>
public sealed class PartyAnswerService(PartyRegistry parties, ILogger<PartyAnswerService> logger)
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

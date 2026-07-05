using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     On accept, collapses legacy's separate PARTY_JOIN/PARTY_INFO emissions into one fan-out; a full party
///     (<see cref="PartyJoinOutcome.PartyWasFull" />) is a silent no-op.
/// </summary>
public sealed class PartyAnswerService(PartyRegistry parties) : IPartyAnswerService
{
    public PartyAnswerResult Answer(int inviteeId, int answer)
    {
        if (answer is not (0 or 1 or 2))
            return new PartyAnswerResult(PartyAnswerResultKind.NotFound);

        var accepted = answer == 0;
        if (!parties.TryAnswer(inviteeId, accepted, out var inviterId, out var joinOutcome))
            return new PartyAnswerResult(PartyAnswerResultKind.NotFound);

        var members = accepted && joinOutcome != PartyJoinOutcome.PartyWasFull
            ? parties.GetMembers(inviterId)
            : [];

        return new PartyAnswerResult(PartyAnswerResultKind.Answered, inviterId, accepted, joinOutcome, members);
    }
}

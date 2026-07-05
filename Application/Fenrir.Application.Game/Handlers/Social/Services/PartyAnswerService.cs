using Fenrir.Application.Game.Social.Party;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Discriminator for how a CZ_PARTY_ANSWER_SEND attempt resolved.</summary>
public enum PartyAnswerResultKind
{
    NotFound,
    Answered
}

public readonly record struct PartyAnswerResult(
    PartyAnswerResultKind Kind,
    int InviterId = 0,
    bool Accepted = false,
    PartyJoinOutcome JoinOutcome = default,
    IReadOnlyList<int>? Members = null)
{
    public IReadOnlyList<int> Members { get; init; } = Members ?? [];
}

public interface IPartyAnswerService
{
    PartyAnswerResult Answer(int inviteeId, int answer);
}

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

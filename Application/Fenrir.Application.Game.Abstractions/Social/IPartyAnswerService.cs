using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Abstractions.Social;

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
    public PartyAnswerResult Answer(int inviteeId, int answer);
}

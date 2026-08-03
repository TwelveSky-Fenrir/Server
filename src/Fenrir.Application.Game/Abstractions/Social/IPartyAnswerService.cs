using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Abstractions.Social;

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
    IReadOnlyList<PartyMember>? Members = null)
{
    public IReadOnlyList<PartyMember> Members { get; init; } = Members ?? [];
}

public interface IPartyAnswerService
{
    public PartyAnswerResult Answer(int inviteeId, int answer);
}

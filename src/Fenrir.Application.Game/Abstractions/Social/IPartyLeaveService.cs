using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct PartyLeaveResult(
    bool Handled,
    IReadOnlyList<PartyMember>? MembersBeforeLeave = null,
    bool Disbanded = false,
    IReadOnlyList<PartyMember>? RemainingMembers = null)
{
    public IReadOnlyList<PartyMember> MembersBeforeLeave { get; init; } = MembersBeforeLeave ?? [];
    public IReadOnlyList<PartyMember> RemainingMembers { get; init; } = RemainingMembers ?? [];
}

public interface IPartyLeaveService
{
    public PartyLeaveResult Leave(int characterId);
}

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Result of a CZ_PARTY_LEAVE_SEND attempt.</summary>
public readonly record struct PartyLeaveResult(
    bool Handled,
    IReadOnlyList<int>? MembersBeforeLeave = null,
    bool Disbanded = false,
    IReadOnlyList<int>? RemainingMembers = null)
{
    public IReadOnlyList<int> MembersBeforeLeave { get; init; } = MembersBeforeLeave ?? [];
    public IReadOnlyList<int> RemainingMembers { get; init; } = RemainingMembers ?? [];
}

public interface IPartyLeaveService
{
    public PartyLeaveResult Leave(int characterId);
}

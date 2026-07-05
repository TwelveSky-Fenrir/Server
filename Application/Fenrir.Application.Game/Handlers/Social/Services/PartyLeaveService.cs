using Fenrir.Application.Game.Social.Party;

namespace Fenrir.Application.Game.Handlers.Social.Services;

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
    PartyLeaveResult Leave(int characterId);
}

/// <summary>
///     No-op if not partied or is the leader (leader must use CZ_PARTY_BREAK_SEND). Deviation: dropping to 1
///     member auto-disbands here; legacy leaves a lone leader "partied" until an explicit Break.
/// </summary>
public sealed class PartyLeaveService(PartyRegistry parties) : IPartyLeaveService
{
    public PartyLeaveResult Leave(int characterId)
    {
        if (!parties.TryLeave(characterId, out var membersBeforeLeave, out var disbanded))
            return new PartyLeaveResult(false);

        var remaining = disbanded ? [] : parties.GetMembers(membersBeforeLeave[0]);

        return new PartyLeaveResult(true, membersBeforeLeave, disbanded, remaining);
    }
}

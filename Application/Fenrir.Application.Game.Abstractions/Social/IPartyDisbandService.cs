namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Result of a CZ_PARTY_BREAK_SEND -- the members that were in the party before it was disbanded.</summary>
public readonly record struct PartyDisbandResult(IReadOnlyList<int> Members);

public interface IPartyDisbandService
{
    public PartyDisbandResult Disband(int leaderId);
}

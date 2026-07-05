using Fenrir.Application.Game.Social.Party;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Result of a CZ_PARTY_BREAK_SEND -- the members that were in the party before it was disbanded.</summary>
public readonly record struct PartyDisbandResult(IReadOnlyList<int> Members);

public interface IPartyDisbandService
{
    PartyDisbandResult Disband(int leaderId);
}

/// <summary>Leader-only, unconditional full disband.</summary>
public sealed class PartyDisbandService(PartyRegistry parties) : IPartyDisbandService
{
    public PartyDisbandResult Disband(int leaderId)
    {
        return new PartyDisbandResult(parties.Disband(leaderId));
    }
}

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>Leader-only, unconditional full disband.</summary>
public sealed class PartyDisbandService(PartyRegistry parties) : IPartyDisbandService
{
    public PartyDisbandResult Disband(int leaderId)
    {
        return new PartyDisbandResult(parties.Disband(leaderId));
    }
}

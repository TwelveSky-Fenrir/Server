using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct PartyDisbandResult(IReadOnlyList<PartyMember> Members);

public interface IPartyDisbandService
{
    public PartyDisbandResult Disband(int leaderId);
}

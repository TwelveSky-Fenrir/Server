namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct PartyDisbandResult(IReadOnlyList<int> Members);

public interface IPartyDisbandService
{
    public PartyDisbandResult Disband(int leaderId);
}

using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Abstractions.Social;

public enum PartyKickResultKind
{
    NotLeader,
    TargetNotFound,
    Kicked
}

public readonly record struct PartyKickResult(
    PartyKickResultKind Kind,
    int TargetId = 0,
    IReadOnlyList<PartyMember>? MembersBeforeKick = null,
    bool Disbanded = false,
    IReadOnlyList<PartyMember>? RemainingMembers = null)
{
    public IReadOnlyList<PartyMember> MembersBeforeKick { get; init; } = MembersBeforeKick ?? [];
    public IReadOnlyList<PartyMember> RemainingMembers { get; init; } = RemainingMembers ?? [];
}

public interface IPartyKickService
{
    public PartyKickResult Kick(int leaderId, string targetAvatarName);
}

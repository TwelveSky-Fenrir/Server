namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Discriminator for how a CZ_PARTY_EXILE_SEND attempt resolved.</summary>
public enum PartyKickResultKind
{
    NotLeader,
    TargetNotFound,
    Kicked
}

public readonly record struct PartyKickResult(
    PartyKickResultKind Kind,
    int TargetId = 0,
    IReadOnlyList<int>? MembersBeforeKick = null,
    bool Disbanded = false,
    IReadOnlyList<int>? RemainingMembers = null)
{
    public IReadOnlyList<int> MembersBeforeKick { get; init; } = MembersBeforeKick ?? [];
    public IReadOnlyList<int> RemainingMembers { get; init; } = RemainingMembers ?? [];
}

public interface IPartyKickService
{
    public PartyKickResult Kick(int leaderId, string targetAvatarName);
}

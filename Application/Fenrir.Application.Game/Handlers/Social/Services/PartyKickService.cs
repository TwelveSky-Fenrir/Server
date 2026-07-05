using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.Social.Services;

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
    PartyKickResult Kick(int leaderId, string targetAvatarName);
}

/// <summary>A self-targeted kick isn't specially guarded, matching legacy's own lack of a guard.</summary>
public sealed class PartyKickService(ZoneRegistry zones, PartyRegistry parties) : IPartyKickService
{
    public PartyKickResult Kick(int leaderId, string targetAvatarName)
    {
        if (!parties.IsLeader(leaderId))
            return new PartyKickResult(PartyKickResultKind.NotLeader);

        var currentMembers = parties.GetMembers(leaderId);
        var targetId = 0;
        foreach (var memberId in currentMembers)
            if (zones.TryGetPlayer(memberId, out var member) &&
                string.Equals(member.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                targetId = memberId;
                break;
            }

        if (targetId == 0 || !parties.TryKick(leaderId, targetId, out var membersBeforeKick, out var disbanded))
            return new PartyKickResult(PartyKickResultKind.TargetNotFound);

        if (disbanded)
            return new PartyKickResult(PartyKickResultKind.Kicked, targetId, membersBeforeKick, true);

        // Anchor on a surviving member, not leaderId: a self-kick removes leaderId from the roster index too.
        var anchor = membersBeforeKick.FirstOrDefault(id => id != targetId);
        var remaining = parties.GetMembers(anchor);

        return new PartyKickResult(PartyKickResultKind.Kicked, targetId, membersBeforeKick, false, remaining);
    }
}

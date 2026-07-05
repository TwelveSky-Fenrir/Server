using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Social;

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

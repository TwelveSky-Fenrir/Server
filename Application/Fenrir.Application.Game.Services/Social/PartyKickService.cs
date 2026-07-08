using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>A self-targeted kick isn't specially guarded, matching legacy's own lack of a guard.</summary>
public sealed class PartyKickService(ZoneRegistry zones, PartyRegistry parties, ILogger<PartyKickService> logger)
    : IPartyKickService
{
    public PartyKickResult Kick(int leaderId, string targetAvatarName)
    {
        if (!parties.IsLeader(leaderId))
        {
            // A non-leader (or non-partied) character attempting a leader-only action -- either a stale UI
            // state or a spoofed/tampered request, worth surfacing by default unlike routine business rejections.
            logger.LogWarning(
                "Party kick rejected: character {LeaderId} is not the leader of a party, cannot kick {TargetAvatarName}",
                leaderId, targetAvatarName);
            return new PartyKickResult(PartyKickResultKind.NotLeader);
        }

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
        {
            logger.LogDebug(
                "Party kick rejected: leader {LeaderId} target {TargetAvatarName} is not a member of the party",
                leaderId, targetAvatarName);
            return new PartyKickResult(PartyKickResultKind.TargetNotFound);
        }

        if (disbanded)
        {
            logger.LogInformation(
                "Party disbanded: leader {LeaderId} kicked character {TargetId}, dropping the party below 2 members",
                leaderId, targetId);
            return new PartyKickResult(PartyKickResultKind.Kicked, targetId, membersBeforeKick, true);
        }

        // Anchor on a surviving member, not leaderId: a self-kick removes leaderId from the roster index too.
        var anchor = membersBeforeKick.FirstOrDefault(id => id != targetId);
        var remaining = parties.GetMembers(anchor);

        logger.LogInformation(
            "Party member kicked: leader {LeaderId} kicked character {TargetId}, {RemainingCount} members remain",
            leaderId, targetId, remaining.Count);

        return new PartyKickResult(PartyKickResultKind.Kicked, targetId, membersBeforeKick, false, remaining);
    }
}

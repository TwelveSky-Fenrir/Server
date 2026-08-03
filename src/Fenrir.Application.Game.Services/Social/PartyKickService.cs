using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class PartyKickService(PartyRegistry parties, ILogger<PartyKickService> logger)
    : IPartyKickService
{
    public PartyKickResult Kick(int leaderId, string targetAvatarName)
    {
        if (!parties.IsLeader(leaderId))
        {
            logger.LogWarning(
                "Party kick rejected: character {LeaderId} is not the leader of a party, cannot kick {TargetAvatarName}",
                leaderId, targetAvatarName);
            return new PartyKickResult(PartyKickResultKind.NotLeader);
        }

        if (!parties.TryResolveMemberByName(leaderId, targetAvatarName, out var targetId) ||
            !parties.TryKick(leaderId, targetId, out var membersBeforeKick, out var disbanded))
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

        var remaining = parties.GetRoster(leaderId);

        logger.LogInformation(
            "Party member kicked: leader {LeaderId} kicked character {TargetId}, {RemainingCount} members remain",
            leaderId, targetId, remaining.Count);

        return new PartyKickResult(PartyKickResultKind.Kicked, targetId, membersBeforeKick, false, remaining);
    }
}

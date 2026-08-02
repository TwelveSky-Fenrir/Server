using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorCancelService(
    MentorRegistry mentors,
    ZoneRegistry zones,
    ILogger<MentorCancelService> logger)
    : IMentorCancelService
{
    public MentorCancelResult Cancel(int masterId)
    {
        if (!mentors.TryWithdrawAsk(masterId, out var studentId))
        {
            logger.LogDebug("Mentor cancel ignored: character {MasterId} has no pending ask to cancel", masterId);
            return new MentorCancelResult(false, 0);
        }

        if (!zones.TryGetPlayer(studentId, out var student) || student.IsMovingZone)
        {
            logger.LogDebug(
                "Mentor cancel: character {MasterId} withdrew its own pending ask, but counterpart {StudentId} is unreachable or mid zone-transfer -- no notice sent, counterpart's record left as-is",
                masterId, studentId);
            return new MentorCancelResult(false, 0);
        }

        if (!mentors.TryAcknowledgeWithdrawal(studentId, masterId))
        {
            logger.LogDebug(
                "Mentor cancel: character {MasterId} withdrew its own pending ask, but counterpart {StudentId}'s own negotiation state no longer matches -- no notice sent",
                masterId, studentId);
            return new MentorCancelResult(false, 0);
        }

        logger.LogDebug("Mentor ask cancelled: character {MasterId} withdrew ask to character {StudentId}",
            masterId, studentId);
        return new MentorCancelResult(true, studentId);
    }
}

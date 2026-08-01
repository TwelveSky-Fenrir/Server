using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorCancelService(MentorRegistry mentors, ILogger<MentorCancelService> logger)
    : IMentorCancelService
{
    public MentorCancelResult Cancel(int masterId)
    {
        if (!mentors.TryCancel(masterId, out var studentId))
        {
            logger.LogDebug("Mentor cancel ignored: character {MasterId} has no pending ask to cancel", masterId);
            return new MentorCancelResult(false, 0);
        }

        logger.LogDebug("Mentor ask cancelled: character {MasterId} withdrew ask to character {StudentId}",
            masterId, studentId);
        return new MentorCancelResult(true, studentId);
    }
}

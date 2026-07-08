using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorAnswerService(MentorRegistry mentors, ILogger<MentorAnswerService> logger)
    : IMentorAnswerService
{
    public MentorAnswerResult Answer(int studentId, int answer)
    {
        if (answer is not (0 or 1 or 2))
        {
            logger.LogDebug("Mentor answer rejected: character {StudentId} sent malformed answer code {Answer}",
                studentId, answer);
            return new MentorAnswerResult(false, 0);
        }

        if (!mentors.TryAnswer(studentId, answer == 0, out var masterId))
        {
            logger.LogDebug("Mentor answer ignored: character {StudentId} has no pending ask", studentId);
            return new MentorAnswerResult(false, 0);
        }

        logger.LogDebug("Mentor answer: character {StudentId} answered {Answer} to master {MasterId}", studentId,
            answer, masterId);
        return new MentorAnswerResult(true, masterId);
    }
}

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorAnswerService(
    MentorRegistry mentors,
    ZoneRegistry zones,
    ILogger<MentorAnswerService> logger)
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

        var accepted = answer == 0;

        if (!mentors.TryPeekPending(studentId, out var pendingMasterId, out var isMaster) || isMaster)
        {
            logger.LogDebug("Mentor answer ignored: character {StudentId} has no pending ask", studentId);
            return new MentorAnswerResult(false, 0);
        }

        var masterBusyByZoneTransfer = !zones.TryGetPlayer(pendingMasterId, out var master) || master.IsMovingZone;

        if (!mentors.TryAnswer(studentId, accepted, masterBusyByZoneTransfer, out var masterId, out var guardBlocked))
        {
            if (guardBlocked)
                logger.LogDebug(
                    "Mentor answer: character {StudentId} answered but counterpart (master) {MasterId} is unreachable or mid zone-transfer -- no notice sent, master's negotiation state left as-is",
                    studentId, masterId);
            else
                logger.LogDebug("Mentor answer ignored: character {StudentId} has no pending ask", studentId);

            return new MentorAnswerResult(false, 0);
        }

        logger.LogDebug("Mentor answer: character {StudentId} answered {Answer} to master {MasterId}", studentId,
            answer, masterId);
        return new MentorAnswerResult(true, masterId);
    }
}

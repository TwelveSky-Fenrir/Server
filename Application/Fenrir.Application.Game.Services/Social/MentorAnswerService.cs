using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Mentor;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorAnswerService(MentorRegistry mentors) : IMentorAnswerService
{
    public MentorAnswerResult Answer(int studentId, int answer)
    {
        if (answer is not (0 or 1 or 2))
            return new MentorAnswerResult(false, 0);

        if (!mentors.TryAnswer(studentId, answer == 0, out var masterId))
            return new MentorAnswerResult(false, 0);

        return new MentorAnswerResult(true, masterId);
    }
}

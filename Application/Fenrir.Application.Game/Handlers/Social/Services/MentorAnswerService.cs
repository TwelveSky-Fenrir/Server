using Fenrir.Application.Game.Social.Mentor;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Outcome of a CZ_TEACHER_ANSWER_SEND negotiation attempt.</summary>
public readonly record struct MentorAnswerResult(bool Handled, int MasterId);

public interface IMentorAnswerService
{
    MentorAnswerResult Answer(int studentId, int answer);
}

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

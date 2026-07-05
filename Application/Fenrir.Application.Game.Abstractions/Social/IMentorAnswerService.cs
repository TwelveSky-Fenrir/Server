namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Outcome of a CZ_TEACHER_ANSWER_SEND negotiation attempt.</summary>
public readonly record struct MentorAnswerResult(bool Handled, int MasterId);

public interface IMentorAnswerService
{
    public MentorAnswerResult Answer(int studentId, int answer);
}

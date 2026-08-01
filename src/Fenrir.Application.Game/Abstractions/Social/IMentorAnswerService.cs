namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct MentorAnswerResult(bool Handled, int MasterId);

public interface IMentorAnswerService
{
    public MentorAnswerResult Answer(int studentId, int answer);
}

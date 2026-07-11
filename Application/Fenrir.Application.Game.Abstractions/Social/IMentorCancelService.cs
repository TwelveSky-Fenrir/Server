namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct MentorCancelResult(bool Handled, int StudentId);

public interface IMentorCancelService
{
    public MentorCancelResult Cancel(int masterId);
}

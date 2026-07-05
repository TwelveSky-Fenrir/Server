namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Outcome of a CZ_TEACHER_CANCEL_SEND attempt.</summary>
public readonly record struct MentorCancelResult(bool Handled, int StudentId);

public interface IMentorCancelService
{
    public MentorCancelResult Cancel(int masterId);
}

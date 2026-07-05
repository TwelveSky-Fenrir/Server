using Fenrir.Application.Game.Social.Mentor;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Outcome of a CZ_TEACHER_CANCEL_SEND attempt.</summary>
public readonly record struct MentorCancelResult(bool Handled, int StudentId);

public interface IMentorCancelService
{
    MentorCancelResult Cancel(int masterId);
}

public sealed class MentorCancelService(MentorRegistry mentors) : IMentorCancelService
{
    public MentorCancelResult Cancel(int masterId)
    {
        return mentors.TryCancel(masterId, out var studentId)
            ? new MentorCancelResult(true, studentId)
            : new MentorCancelResult(false, 0);
    }
}

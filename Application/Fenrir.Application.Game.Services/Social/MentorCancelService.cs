using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Mentor;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorCancelService(MentorRegistry mentors) : IMentorCancelService
{
    public MentorCancelResult Cancel(int masterId)
    {
        return mentors.TryCancel(masterId, out var studentId)
            ? new MentorCancelResult(true, studentId)
            : new MentorCancelResult(false, 0);
    }
}

using Fenrir.Application.Game.Domain.Social.Mentor;

namespace Fenrir.Application.Game.Tests.Social;

public class MentorRegistryTests
{
    [Fact]
    public void TryAsk_ThenAccept_OnlyMasterCanConsumeStart()
    {
        var registry = new MentorRegistry();

        Assert.Equal(MentorAskOutcome.Sent,
            registry.TryAsk(1, 2, false, false));
        Assert.True(registry.TryAnswer(2, true, out var masterId));
        Assert.Equal(1, masterId);

        Assert.True(registry.TryConsumeStart(1, out var studentId));
        Assert.Equal(2, studentId);
    }

    [Fact]
    public void TryConsumeStart_ByStudent_Fails()
    {
        var registry = new MentorRegistry();
        registry.TryAsk(1, 2, false, false);
        registry.TryAnswer(2, true, out _);

        Assert.False(registry.TryConsumeStart(2, out _));
    }

    [Fact]
    public void TryAsk_TargetAlreadyHasTeacherOrStudent_ReturnsExpectedOutcome()
    {
        var registry = new MentorRegistry();

        Assert.Equal(MentorAskOutcome.TargetAlreadyHasTeacher,
            registry.TryAsk(1, 2, true, false));
        Assert.Equal(MentorAskOutcome.TargetAlreadyHasStudent,
            registry.TryAsk(1, 3, false, true));
    }

    [Fact]
    public void TryConsumeStart_IsOneShot()
    {
        var registry = new MentorRegistry();
        registry.TryAsk(1, 2, false, false);
        registry.TryAnswer(2, true, out _);
        registry.TryConsumeStart(1, out _);

        Assert.False(registry.TryConsumeStart(1, out _));
    }
}

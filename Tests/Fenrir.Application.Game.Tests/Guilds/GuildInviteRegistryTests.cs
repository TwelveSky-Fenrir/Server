using Fenrir.Application.Game.Guilds;

namespace Fenrir.Application.Game.Tests.Guilds;

public class GuildInviteRegistryTests
{
    [Fact]
    public void TryAsk_ThenAccept_AskerCanConsumeTheAcceptedInvite()
    {
        var registry = new GuildInviteRegistry();

        Assert.Equal(GuildInviteAskOutcome.Sent, registry.TryAsk(1, 2));
        Assert.True(registry.TryAnswer(2, true, out var askerId));
        Assert.Equal(1, askerId);

        Assert.True(registry.TryConsumeAccepted(1, out var targetId));
        Assert.Equal(2, targetId);
    }

    [Fact]
    public void TryConsumeAccepted_IsOneShot()
    {
        var registry = new GuildInviteRegistry();
        registry.TryAsk(1, 2);
        registry.TryAnswer(2, true, out _);

        Assert.True(registry.TryConsumeAccepted(1, out _));
        Assert.False(registry.TryConsumeAccepted(1, out _));
    }

    [Fact]
    public void TryConsumeAccepted_OnlyTheAsker_NotTheTarget_CanConsumeIt()
    {
        var registry = new GuildInviteRegistry();
        registry.TryAsk(1, 2);
        registry.TryAnswer(2, true, out _);

        // GUILD_WORK tSort 3 is finalized by the ASKER, never the target (doc 10 §1 tSort 3).
        Assert.False(registry.TryConsumeAccepted(2, out _));
    }

    [Fact]
    public void TryAsk_AlreadyNegotiating_ReturnsBusy()
    {
        var registry = new GuildInviteRegistry();
        registry.TryAsk(1, 2);

        Assert.Equal(GuildInviteAskOutcome.AskerBusy, registry.TryAsk(1, 3));
        Assert.Equal(GuildInviteAskOutcome.TargetBusy, registry.TryAsk(4, 2));
    }

    [Fact]
    public void TryAnswer_Refused_NeverBecomesAccepted()
    {
        var registry = new GuildInviteRegistry();
        registry.TryAsk(1, 2);

        Assert.True(registry.TryAnswer(2, false, out _));
        Assert.False(registry.TryConsumeAccepted(1, out _));
    }

    [Fact]
    public void TryCancel_ClearsPendingAsk_AllowingTheTargetToBeAskedAgain()
    {
        var registry = new GuildInviteRegistry();
        registry.TryAsk(1, 2);

        Assert.True(registry.TryCancel(1, out var targetId));
        Assert.Equal(2, targetId);
        Assert.Equal(GuildInviteAskOutcome.Sent, registry.TryAsk(4, 2));
    }

    [Fact]
    public void TryCancel_NotCurrentlyAsking_ReturnsFalse()
    {
        var registry = new GuildInviteRegistry();

        Assert.False(registry.TryCancel(1, out _));
    }

    [Fact]
    public void TryAnswer_NoPendingAskForThisTarget_ReturnsFalse()
    {
        var registry = new GuildInviteRegistry();

        Assert.False(registry.TryAnswer(2, true, out _));
    }
}

using Fenrir.Application.Game.Domain.Social.Friends;

namespace Fenrir.Application.Game.Tests.Social;

public class FriendRegistryTests
{
    [Fact]
    public void TryAsk_ThenAccept_BothSidesCanConsumeTheirOwnMake()
    {
        var registry = new FriendRegistry();

        Assert.Equal(FriendAskOutcome.Sent, registry.TryAsk(1, 2));
        Assert.True(registry.TryAnswer(2, true, out var askerId));
        Assert.Equal(1, askerId);

        // ONE-DIRECTIONAL: each side independently consumes its own accepted flag.
        Assert.True(registry.TryConsumeAccepted(1, out var otherForAsker));
        Assert.Equal(2, otherForAsker);
        Assert.True(registry.TryConsumeAccepted(2, out var otherForTarget));
        Assert.Equal(1, otherForTarget);
    }

    [Fact]
    public void TryConsumeAccepted_IsOneShot()
    {
        var registry = new FriendRegistry();
        registry.TryAsk(1, 2);
        registry.TryAnswer(2, true, out _);

        Assert.True(registry.TryConsumeAccepted(1, out _));
        Assert.False(registry.TryConsumeAccepted(1, out _));
    }

    [Fact]
    public void TryAsk_AlreadyNegotiating_ReturnsBusy()
    {
        var registry = new FriendRegistry();
        registry.TryAsk(1, 2);

        Assert.Equal(FriendAskOutcome.AskerBusy, registry.TryAsk(1, 3));
        Assert.Equal(FriendAskOutcome.TargetBusy, registry.TryAsk(4, 2));
    }

    [Fact]
    public void TryAnswer_Refused_NeverBecomesAccepted()
    {
        var registry = new FriendRegistry();
        registry.TryAsk(1, 2);

        Assert.True(registry.TryAnswer(2, false, out _));
        Assert.False(registry.TryConsumeAccepted(1, out _));
        Assert.False(registry.TryConsumeAccepted(2, out _));
    }

    [Fact]
    public void TryCancel_ClearsPendingAskForBothSides()
    {
        var registry = new FriendRegistry();
        registry.TryAsk(1, 2);

        Assert.True(registry.TryCancel(1, out var targetId));
        Assert.Equal(2, targetId);
        Assert.Equal(FriendAskOutcome.Sent, registry.TryAsk(4, 2));
    }
}

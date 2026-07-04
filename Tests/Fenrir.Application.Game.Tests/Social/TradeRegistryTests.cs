using Fenrir.Application.Game.Social.Trade;

namespace Fenrir.Application.Game.Tests.Social;

public class TradeRegistryTests
{
    [Fact]
    public void FullLifecycle_AskAcceptStart_AllocatesSharedEmptySession()
    {
        var registry = new TradeRegistry();

        Assert.Equal(TradeAskOutcome.Sent, registry.TryAsk(1, 2));
        Assert.True(registry.TryAnswer(2, accepted: true, out var askerId));
        Assert.Equal(1, askerId);

        Assert.True(registry.TryStart(1, out var session));
        Assert.Equal(1, session.PlayerAId);
        Assert.Equal(2, session.PlayerBId);
        Assert.Equal(0, session.SideA.Money);
        Assert.Equal(0, session.SideB.Money);

        Assert.True(registry.TryGetSession(1, out var fromA));
        Assert.True(registry.TryGetSession(2, out var fromB));
        Assert.Same(fromA, fromB);
    }

    [Fact]
    public void TryEnd_RemovesBothParticipants()
    {
        var registry = new TradeRegistry();
        registry.TryAsk(1, 2);
        registry.TryAnswer(2, true, out _);
        registry.TryStart(1, out _);

        Assert.True(registry.TryEnd(1, out var session));
        Assert.NotNull(session);
        Assert.False(registry.TryGetSession(1, out _));
        Assert.False(registry.TryGetSession(2, out _));
    }

    [Fact]
    public void TryAsk_AlreadyTrading_ReturnsBusy()
    {
        var registry = new TradeRegistry();
        registry.TryAsk(1, 2);
        registry.TryAnswer(2, true, out _);
        registry.TryStart(1, out _);

        Assert.Equal(TradeAskOutcome.AskerBusy, registry.TryAsk(1, 3));
        Assert.Equal(TradeAskOutcome.TargetBusy, registry.TryAsk(4, 2));
    }

    [Fact]
    public void SideOf_And_OpponentSideOf_ResolveCorrectly()
    {
        var registry = new TradeRegistry();
        registry.TryAsk(1, 2);
        registry.TryAnswer(2, true, out _);
        registry.TryStart(1, out var session);

        Assert.Same(session.SideA, session.SideOf(1));
        Assert.Same(session.SideB, session.SideOf(2));
        Assert.Same(session.SideB, session.OpponentSideOf(1));
        Assert.Equal(2, session.OpponentOf(1));
        Assert.Equal(1, session.OpponentOf(2));
    }
}

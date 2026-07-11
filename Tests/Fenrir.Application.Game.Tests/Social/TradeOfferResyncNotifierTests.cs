using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Social;

public class TradeOfferResyncNotifierTests
{
    private static (ZoneRegistry Registry, TradeRegistry Trades, FakeDuplexPipe PipeA, FakeDuplexPipe PipeB)
        SetUp(short mapId)
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([mapId]);
        var zone = registry[mapId];

        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(sessionA, mapId, name: "Alice")));
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(sessionB, mapId, name: "Bob")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        var trades = new TradeRegistry();
        return (registry, trades, pipeA, pipeB);
    }

    private static TradeSession StartSession(TradeRegistry trades, int playerAId, int playerBId)
    {
        trades.TryAsk(playerAId, playerBId);
        trades.TryAnswer(playerBId, true, out _);
        trades.TryStart(playerAId, out var session);
        return session;
    }

    [Fact]
    public void ActiveSession_MutatingSideAddsMoney_OnlyOpponentIsNotified()
    {
        var (registry, trades, pipeA, pipeB) = SetUp(37);
        var session = StartSession(trades, 1, 2);
        session.SideA.Money = 500;

        var handled = TradeOfferResyncNotifier.TryNotifyOpponent(trades, registry, 1);

        Assert.True(handled);
        var toB = ZoneTestKit.DrainOutbound(pipeB);
        Assert.Equal(FrameWriter.FrameSizeOf<TradeUpdateResponse>(), toB.Length);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeA));
    }

    [Fact]
    public void ActiveSession_OpponentMutates_NotifiesTheOriginalAskerInstead()
    {
        var (registry, trades, pipeA, pipeB) = SetUp(37);
        var session = StartSession(trades, 1, 2);
        session.SideB.Money = 750;

        var handled = TradeOfferResyncNotifier.TryNotifyOpponent(trades, registry, 2);

        Assert.True(handled);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeB));
        var toA = ZoneTestKit.DrainOutbound(pipeA);
        Assert.Equal(FrameWriter.FrameSizeOf<TradeUpdateResponse>(), toA.Length);
    }

    [Fact]
    public void NoActiveSession_SilentlyDoesNothing()
    {
        var (registry, trades, pipeA, pipeB) = SetUp(37);

        var handled = TradeOfferResyncNotifier.TryNotifyOpponent(trades, registry, 1);

        Assert.False(handled);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeA));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeB));
    }

    [Fact]
    public void SessionAlreadyEnded_SilentlyDoesNothing()
    {
        var (registry, trades, pipeA, pipeB) = SetUp(37);
        StartSession(trades, 1, 2);
        trades.TryEnd(1, out _);

        var handled = TradeOfferResyncNotifier.TryNotifyOpponent(trades, registry, 1);

        Assert.False(handled);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeA));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeB));
    }

    [Fact]
    public void OpponentNoLongerOnline_SilentlyDoesNothing()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([(short)37]);
        var zone = registry[(short)37];

        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(sessionA, 37, name: "Alice")));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipeA);

        var trades = new TradeRegistry();
        StartSession(trades, 1, 2);

        var handled = TradeOfferResyncNotifier.TryNotifyOpponent(trades, registry, 1);

        Assert.False(handled);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeA));
    }
}

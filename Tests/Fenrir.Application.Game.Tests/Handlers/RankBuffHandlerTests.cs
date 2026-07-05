using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class RankBuffHandlerTests
{
    private static readonly int StatUpdateFrame = FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    [Fact]
    public void Sort1_DefaultStoneCount_HealsAndMirrorsRankBuffType()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;

        var handler = new RankBuffHandler();
        handler.Handle(new RankBuffRequest { Sort = 1 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(1, player!.RankBuffType);
        Assert.Equal(800, player.Life);
        Assert.Equal(300, player.Mana);
        Assert.Equal(StatUpdateFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void Sort2_InsufficientDefaultStoneCount_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 10);
        var handler = new RankBuffHandler();

        handler.Handle(new RankBuffRequest { Sort = 2 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public void OutOfRangeSort_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 10);
        var handler = new RankBuffHandler();

        handler.Handle(new RankBuffRequest { Sort = 8 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }
}

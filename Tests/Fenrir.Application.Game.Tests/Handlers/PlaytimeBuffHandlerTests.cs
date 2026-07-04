using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class PlaytimeBuffHandlerTests
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
    public void ValidSort_MirrorsStateTimeEffectAndReplies()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var handler = new PlaytimeBuffHandler();

        handler.Handle(new PlaytimeBuffRequest { Sort = 3 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(240, player!.StateTimeEffect);
        Assert.Equal(StatUpdateFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void OutOfRangeSort_NoMirror_StillReplies()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var handler = new PlaytimeBuffHandler();

        handler.Handle(new PlaytimeBuffRequest { Sort = 9 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(0, player!.StateTimeEffect);
        Assert.Equal(StatUpdateFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }
}

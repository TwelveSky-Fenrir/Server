using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class CostumeVisibilityHandlerTests
{
    private static readonly int VisibilityFrame = FrameWriter.FrameSizeOf<CostumeVisibilityResponse>();
    private static readonly int ActionFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId, float posX = 10f, float posZ = 10f)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, zone.MapId, posX: posX, posZ: posZ)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ValidSort_SetsStateAndRebroadcastsFullAction(int sort)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var (_, neighborPipe, _) = Setup(zone, 20, 12f, 12f);
        ZoneTestKit.DrainOutbound(pipe); // neighbor's own Enter-broadcast join packet, not under test
        var handler = new CostumeVisibilityHandler(new CostumeVisibilityService());

        handler.Handle(new CostumeVisibilityRequest { Sort = sort }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(sort, player!.CostumeState);

        Assert.Equal(VisibilityFrame + ActionFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(ActionFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public void InvalidSort_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var handler = new CostumeVisibilityHandler(new CostumeVisibilityService());

        handler.Handle(new CostumeVisibilityRequest { Sort = 2 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}

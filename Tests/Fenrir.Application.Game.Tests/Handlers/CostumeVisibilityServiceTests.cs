using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Services.BuffsMountsCosmetics;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Framing;

namespace Fenrir.Application.Game.Tests.Handlers;

public class CostumeVisibilityServiceTests
{
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
        var service = new CostumeVisibilityService();

        service.Apply(zone, 10, sort);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(sort, player!.CostumeState);

        Assert.Equal(ActionFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(ActionFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public void InvalidSort_Aborts()
    {
        // Sort validation (0/1 only) lives on the handler itself, ahead of the service call -- exercise the
        // real handler here rather than the service, which never sees an out-of-range sort at all.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var handler = new CostumeVisibilityHandler(new CostumeVisibilityService());

        handler.Handle(new CostumeVisibilityRequest { Sort = 2 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}

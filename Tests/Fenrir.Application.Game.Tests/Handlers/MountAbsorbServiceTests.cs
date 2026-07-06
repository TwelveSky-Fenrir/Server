using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Services.BuffsMountsCosmetics;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Handlers;

public class MountAbsorbServiceTests
{
    private static readonly int StateFlagFrame = FrameWriter.FrameSizeOf<AvatarStateFlagResponse>();
    private static readonly int StatUpdateFrame = FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();

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

    [Fact]
    public void Enable_NotCurrentlyMounted_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var service = new MountAbsorbService();

        var absorbed = service.TryAbsorb(zone, state, 10);

        Assert.False(absorbed);
    }

    [Fact]
    public void Enable_MountedButNoAbsorbTime_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        state.AnimalIndex = 13;
        var service = new MountAbsorbService();

        var absorbed = service.TryAbsorb(zone, state, 10);

        Assert.False(absorbed);
    }

    [Fact]
    public void Enable_ValidPreconditions_MirrorsStateAndBroadcastsToSelfAndNeighbor()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var (_, neighborPipe, _) = Setup(zone, 20, 12f, 12f);
        ZoneTestKit.DrainOutbound(pipe); // neighbor's own Enter-broadcast join packet, not under test
        state.AnimalIndex = 13;
        state.AnimalAbsorbTime = 5;
        var service = new MountAbsorbService();

        var absorbed = service.TryAbsorb(zone, state, 10);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(absorbed);
        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(1, mover!.AnimalAbsorbState);

        Assert.Equal(StateFlagFrame + StatUpdateFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public void Disable_HealsToMaxAndClearsAbsorbState()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        state.AnimalAbsorbState = 1;
        state.Life = 1;
        state.Mana = 1;
        state.MaxLife = 800;
        state.MaxMana = 300;
        var service = new MountAbsorbService();

        service.Release(zone, state, 10);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(0, mover!.AnimalAbsorbState);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);
        Assert.Equal(StateFlagFrame + StatUpdateFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void UnknownSort_Aborts()
    {
        // Sort 1/2 dispatch to TryAbsorb/Release respectively; any other sort is a handler-owned fallback
        // abort the service itself never sees -- exercise the real handler here.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 10);
        var handler = new MountAbsorbHandler(new MountAbsorbService());

        handler.Handle(new MountAbsorbRequest { Sort = 3 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }
}

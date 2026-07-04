using System.Collections.Immutable;
using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class MountStateHandlerTests
{
    private static readonly int StateResponseFrame = FrameWriter.FrameSizeOf<MountStateResponse>();
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
    public void Select_ValidSlot_RepliesAndMirrorsIndex()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var handler = new MountStateHandler();

        handler.Handle(new MountStateRequest { Sort = 1, Value = 4 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(4, player!.AnimalIndex);
        Assert.Equal(StateResponseFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void Select_SlotOutOfRange_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var handler = new MountStateHandler();

        handler.Handle(new MountStateRequest { Sort = 1, Value = 99 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(-1, player!.AnimalIndex);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Mount_ValidPreconditions_HealsAndBroadcastsMountThenAbsorbReset()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var (_, neighborPipe, _) = Setup(zone, 20, 12f, 12f);
        ZoneTestKit.DrainOutbound(pipe); // neighbor's own Enter-broadcast join packet, not under test
        state.AnimalIndex = 2;
        state.AnimalTime = 5;
        state.ActionSort = 1;
        state.AnimalAbsorbState = 1;
        state.MountGarage = ImmutableArray.Create(0, 0, 1006, 0, 0, 0, 0, 0, 0, 0);
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var handler = new MountStateHandler();

        handler.Handle(new MountStateRequest { Sort = 3, Value = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(12, mover!.AnimalIndex);
        Assert.Equal(1006, mover.AnimalNumber);
        Assert.Equal(0, mover.AnimalAbsorbState);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);

        Assert.Equal(StateResponseFrame + 2 * StateFlagFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(2 * StateFlagFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public void Mount_NotIdle_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        state.AnimalIndex = 2;
        state.AnimalTime = 5;
        state.ActionSort = 0;
        var handler = new MountStateHandler();

        handler.Handle(new MountStateRequest { Sort = 3, Value = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(2, player!.AnimalIndex);
        Assert.Equal(0, player.AnimalNumber);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Dismount_ValidPreconditions_HealsAndBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        state.AnimalIndex = 12;
        state.AnimalNumber = 1006;
        state.AnimalAbsorbState = 1;
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var handler = new MountStateHandler();

        handler.Handle(new MountStateRequest { Sort = 4, Value = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(2, mover!.AnimalIndex);
        Assert.Equal(0, mover.AnimalNumber);
        Assert.Equal(0, mover.AnimalAbsorbState);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(StateResponseFrame + StateFlagFrame + StatUpdateFrame, frame.Length);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(120)]
    public void UnsupportedSort_Aborts(int sort)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 10);
        var handler = new MountStateHandler();

        handler.Handle(new MountStateRequest { Sort = sort, Value = 0 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }
}

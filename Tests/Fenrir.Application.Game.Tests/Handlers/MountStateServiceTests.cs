using System.Collections.Immutable;
using Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class MountStateServiceTests
{
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
        var (session, _, state) = Setup(zone, 10);
        var service = new MountStateService();

        var result = service.Apply(zone, state, 10, 1, 4);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(MountStateOutcome.Select, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(4, player!.AnimalIndex);
    }

    [Fact]
    public void Select_SlotOutOfRange_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var service = new MountStateService();

        var result = service.Apply(zone, state, 10, 1, 99);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(MountStateOutcome.NoReply, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(-1, player!.AnimalIndex);
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
        var service = new MountStateService();

        var result = service.Apply(zone, state, 10, 3, 0);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(MountStateOutcome.Mount, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(12, mover!.AnimalIndex);
        Assert.Equal(1006, mover.AnimalNumber);
        Assert.Equal(0, mover.AnimalAbsorbState);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);
    }

    [Fact]
    public void Mount_NotIdle_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        state.AnimalIndex = 2;
        state.AnimalTime = 5;
        state.ActionSort = 0;
        var service = new MountStateService();

        var result = service.Apply(zone, state, 10, 3, 0);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(MountStateOutcome.NoReply, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(2, player!.AnimalIndex);
        Assert.Equal(0, player.AnimalNumber);
    }

    [Fact]
    public void Dismount_ValidPreconditions_HealsAndBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        state.AnimalIndex = 12;
        state.AnimalNumber = 1006;
        state.AnimalAbsorbState = 1;
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var service = new MountStateService();

        var result = service.Apply(zone, state, 10, 4, 0);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(MountStateOutcome.Dismount, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(2, mover!.AnimalIndex);
        Assert.Equal(0, mover.AnimalNumber);
        Assert.Equal(0, mover.AnimalAbsorbState);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(120)]
    public void UnsupportedSort_Aborts(int sort)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var service = new MountStateService();

        var result = service.Apply(zone, state, 10, sort, 0);

        Assert.Equal(MountStateOutcome.Disconnect, result.Outcome);
    }
}

using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Framing;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>Drives the real <see cref="ContinueSkillUseService" /> (opcode 95) over a real <see cref="Zone" />.</summary>
public class ContinueSkillUseServiceTests
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

    [Fact]
    public void Sort1_ValidPreconditions_ReducesManaAndBroadcastsChannelingActionToSelfAndNeighbor()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var (_, neighborPipe, _) = Setup(zone, 20, 12f, 12f);
        ZoneTestKit.DrainOutbound(pipe);
        state.AutoBuffTime = GameDate.Today();
        state.ActionSort = 1;
        state.Mana = 100;
        var service = new ContinueSkillUseService();

        var result = service.Activate(zone, 10, state, 1);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(AutoBuffActivationResolver.ResultKind.Activate, result.Kind);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(10, player!.Mana);
        Assert.Equal(41, player.ActionSort);

        Assert.Equal(ActionFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(ActionFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public void Sort1_AutoBuffTimeExpired_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var initialMana = state.Mana;
        var service = new ContinueSkillUseService();

        var result = service.Activate(zone, 10, state, 1);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(AutoBuffActivationResolver.ResultKind.NoReply, result.Kind);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(initialMana, player!.Mana);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Sort1_NotIdle_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Setup(zone, 10);
        state.AutoBuffTime = GameDate.Today();
        state.ActionSort = 30;
        state.Mana = 100;
        var service = new ContinueSkillUseService();

        service.Activate(zone, 10, state, 1);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(100, state.Mana);
        Assert.Equal(30, state.ActionSort);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Sort1_NegativeMana_DisconnectsPerLegacyQuirk()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        state.AutoBuffTime = GameDate.Today();
        state.ActionSort = 1;
        state.Mana = -10;
        var service = new ContinueSkillUseService();

        var result = service.Activate(zone, 10, state, 1);

        Assert.Equal(AutoBuffActivationResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void Sort2_IsASilentNoOp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var service = new ContinueSkillUseService();

        var result = service.Activate(zone, 10, state, 2);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(AutoBuffActivationResolver.ResultKind.Tick, result.Kind);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void UnsupportedSort_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Setup(zone, 10);
        var service = new ContinueSkillUseService();

        var result = service.Activate(zone, 10, state, 3);

        Assert.Equal(AutoBuffActivationResolver.ResultKind.Disconnect, result.Kind);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}

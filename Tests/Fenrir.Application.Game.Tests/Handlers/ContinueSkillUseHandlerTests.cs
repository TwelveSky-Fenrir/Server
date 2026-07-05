using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>Drives the real <see cref="ContinueSkillUseHandler" /> (opcode 95) over a real <see cref="Zone" />.</summary>
public class ContinueSkillUseHandlerTests
{
    private static readonly int ActivationFrame = FrameWriter.FrameSizeOf<AutoBuffActivationResponse>();
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

    private static ContinueSkillUseRequest Request(int sort)
    {
        return new ContinueSkillUseRequest { Location = [0f, 0f, 0f], Sort = sort };
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
        var handler = new ContinueSkillUseHandler(new ContinueSkillUseService());

        handler.Handle(Request(1), session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(10, player!.Mana);
        Assert.Equal(41, player.ActionSort);

        Assert.Equal(ActivationFrame + ActionFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(ActionFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public void Sort1_AutoBuffTimeExpired_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var initialMana = state.Mana;
        var handler = new ContinueSkillUseHandler(new ContinueSkillUseService());

        handler.Handle(Request(1), session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(initialMana, player!.Mana);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Sort1_NotIdle_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        state.AutoBuffTime = GameDate.Today();
        state.ActionSort = 30;
        state.Mana = 100;
        var handler = new ContinueSkillUseHandler(new ContinueSkillUseService());

        handler.Handle(Request(1), session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(100, state.Mana);
        Assert.Equal(30, state.ActionSort);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Sort1_NegativeMana_DisconnectsPerLegacyQuirk()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.AutoBuffTime = GameDate.Today();
        state.ActionSort = 1;
        state.Mana = -10;
        var handler = new ContinueSkillUseHandler(new ContinueSkillUseService());

        handler.Handle(Request(1), session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public void Sort2_IsASilentNoOp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var handler = new ContinueSkillUseHandler(new ContinueSkillUseService());

        handler.Handle(Request(2), session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void UnsupportedSort_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var handler = new ContinueSkillUseHandler(new ContinueSkillUseService());

        handler.Handle(Request(3), session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}

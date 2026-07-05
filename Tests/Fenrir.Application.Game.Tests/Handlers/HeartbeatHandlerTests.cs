using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class HeartbeatHandlerTests
{
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

    private static HeartbeatRequest Packet(uint lastSend)
    {
        return new HeartbeatRequest { LastSend = lastSend, Data = new byte[32] };
    }

    [Fact]
    public void FirstHeartbeat_NeverSentBefore_AcceptsAndStampsState()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        Assert.Null(state.PrevSentHeartbeat);
        var handler = new HeartbeatHandler(new HeartbeatService());

        handler.Handle(Packet(1u), session);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1u, state.PrevSentHeartbeat);
        Assert.NotNull(state.LastSentHeartbeat);
    }

    [Fact]
    public void FirstHeartbeatCarryingZero_IsNotMistakenForReplay()
    {
        // Unlike legacy's truthy-zero mPrevSent sentinel, PrevSentHeartbeat is null (not 0) until the first
        // heartbeat -- a legitimate first LastSend of 0 must still be accepted.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var handler = new HeartbeatHandler(new HeartbeatService());

        handler.Handle(Packet(0u), session);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0u, state.PrevSentHeartbeat);
    }

    [Fact]
    public void AdvancingLastSend_AcceptsAndUpdatesPrevSent()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var handler = new HeartbeatHandler(new HeartbeatService());

        handler.Handle(Packet(1u), session);
        handler.Handle(Packet(2u), session);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(2u, state.PrevSentHeartbeat);
    }

    [Fact]
    public void ReplayedLastSend_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var handler = new HeartbeatHandler(new HeartbeatService());

        handler.Handle(Packet(42u), session);
        Assert.Null(session.DisconnectReason);

        // Same LastSend counter sent twice in a row -- a captured-and-replayed frame.
        handler.Handle(Packet(42u), session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public void ReplayAttempt_DoesNotClobberLastKnownGoodHeartbeatTimestamp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var handler = new HeartbeatHandler(new HeartbeatService());

        handler.Handle(Packet(42u), session);
        var afterFirst = state.LastSentHeartbeat;

        handler.Handle(Packet(42u), session);

        Assert.Equal(afterFirst, state.LastSentHeartbeat);
    }

    [Fact]
    public void PlayerNotInZone_NoOpDoesNotThrow()
    {
        var (session, _) = ZoneTestKit.CreateSession(10);
        session.MarkTicketConsumed(1, 10);
        session.MarkRegistering();
        session.MarkInWorld();
        var handler = new HeartbeatHandler(new HeartbeatService());

        handler.Handle(Packet(1u), session);

        Assert.Null(session.DisconnectReason);
    }
}

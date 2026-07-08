using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class HeartbeatServiceTests
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

    [Fact]
    public void FirstHeartbeat_NeverSentBefore_AcceptsAndStampsState()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        Assert.Null(state.PrevSentHeartbeat);
        var service = new HeartbeatService();

        var outcome = service.Process(state, 1u);

        Assert.Equal(HeartbeatOutcome.Accepted, outcome);
        Assert.Equal(1u, state.PrevSentHeartbeat);
        Assert.NotNull(state.LastSentHeartbeat);
    }

    [Fact]
    public void FirstHeartbeatCarryingZero_IsNotMistakenForReplay()
    {
        // Unlike legacy's truthy-zero mPrevSent sentinel, PrevSentHeartbeat is null (not 0) until the first
        // heartbeat -- a legitimate first LastSend of 0 must still be accepted.
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var service = new HeartbeatService();

        var outcome = service.Process(state, 0u);

        Assert.Equal(HeartbeatOutcome.Accepted, outcome);
        Assert.Equal(0u, state.PrevSentHeartbeat);
    }

    [Fact]
    public void AdvancingLastSend_AcceptsAndUpdatesPrevSent()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var service = new HeartbeatService();

        service.Process(state, 1u);
        var outcome = service.Process(state, 2u);

        Assert.Equal(HeartbeatOutcome.Accepted, outcome);
        Assert.Equal(2u, state.PrevSentHeartbeat);
    }

    [Fact]
    public void ReplayedLastSend_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var service = new HeartbeatService();

        var first = service.Process(state, 42u);
        Assert.Equal(HeartbeatOutcome.Accepted, first);

        // Same LastSend counter sent twice in a row -- a captured-and-replayed frame.
        var second = service.Process(state, 42u);

        Assert.Equal(HeartbeatOutcome.Replayed, second);
    }

    [Fact]
    public void ReplayAttempt_DoesNotClobberLastKnownGoodHeartbeatTimestamp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var service = new HeartbeatService();

        service.Process(state, 42u);
        var afterFirst = state.LastSentHeartbeat;

        service.Process(state, 42u);

        Assert.Equal(afterFirst, state.LastSentHeartbeat);
    }

    [Fact]
    public void PlayerNotInZone_NoOpDoesNotThrow()
    {
        // No PlayerRuntimeState exists yet to hand the service -- this guard is handler-owned plumbing, not
        // service business logic, so exercise the real handler here.
        var (session, _) = ZoneTestKit.CreateSession(10);
        session.MarkTicketConsumed(1, 10);
        session.MarkRegistering();
        session.MarkInWorld();
        var handler = new HeartbeatHandler(new HeartbeatService(), NullLogger<HeartbeatHandler>.Instance);

        handler.Handle(new HeartbeatRequest { LastSend = 1u, Data = new byte[32] }, session);

        Assert.Null(session.DisconnectReason);
    }
}

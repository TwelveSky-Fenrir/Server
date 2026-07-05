using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Application.Game.Tests.Handlers;

public class ZoneReadyServiceTests
{
    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId, byte tribe = 1)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId, tribe: tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    [Fact]
    public void ValidHandshake_AdmitsAndStampsConnectTime()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10, tribe: 2);
        var service = new ZoneReadyService();

        var result = service.Validate(state, 2, 0);

        Assert.Equal(ZoneReadyOutcome.Admitted, result);
        Assert.NotNull(state.ConnectTime);
        Assert.Equal(0, state.AutoTimeHack);
    }

    [Fact]
    public void TribeMismatch_Rejects()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10, tribe: 1);
        var service = new ZoneReadyService();

        // Client claims tribe 3 while the world-entry snapshot loaded tribe 1 -- a patched client.
        var result = service.Validate(state, 3, 0);

        Assert.Equal(ZoneReadyOutcome.Rejected, result);
        Assert.Null(state.ConnectTime);
    }

    [Fact]
    public void StaleHeartbeat_Rejects()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10, tribe: 1);
        state.LastSentHeartbeat = DateTime.UtcNow.AddSeconds(-11);
        var service = new ZoneReadyService();

        var result = service.Validate(state, 1, 0);

        Assert.Equal(ZoneReadyOutcome.Rejected, result);
    }

    [Fact]
    public void FreshHeartbeat_Admits()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10, tribe: 1);
        state.LastSentHeartbeat = DateTime.UtcNow.AddSeconds(-1);
        var service = new ZoneReadyService();

        var result = service.Validate(state, 1, 0);

        Assert.Equal(ZoneReadyOutcome.Admitted, result);
    }

    [Fact]
    public void NeverSentHeartbeat_SkipsWatchdogGuard_Admits()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10, tribe: 1);
        Assert.Null(state.LastSentHeartbeat); // never sent, matching legacy's mLastSentHeartbeat == -1
        var service = new ZoneReadyService();

        var result = service.Validate(state, 1, 0);

        Assert.Equal(ZoneReadyOutcome.Admitted, result);
    }

    [Fact]
    public void AutoStateClaimedWithoutServerAutoHunt_RejectsOnThirdStrike()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10, tribe: 1);
        state.AutoHuntEnabled = false;
        var service = new ZoneReadyService();

        Assert.Equal(ZoneReadyOutcome.Admitted, service.Validate(state, 1, 1));
        Assert.Equal(1, state.AutoTimeHack);

        Assert.Equal(ZoneReadyOutcome.Admitted, service.Validate(state, 1, 1));
        Assert.Equal(2, state.AutoTimeHack);

        Assert.Equal(ZoneReadyOutcome.Rejected, service.Validate(state, 1, 1));
        Assert.Equal(3, state.AutoTimeHack);
    }

    [Fact]
    public void AutoStateClaimedWithServerAutoHuntEnabled_NeverStrikes()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10, tribe: 1);
        state.AutoHuntEnabled = true;
        var service = new ZoneReadyService();

        var result = service.Validate(state, 1, 1);

        Assert.Equal(ZoneReadyOutcome.Admitted, result);
        Assert.Equal(0, state.AutoTimeHack);
    }

    [Fact]
    public void AutoStateZero_NeverStrikesRegardlessOfServerAutoHunt()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10, tribe: 1);
        state.AutoHuntEnabled = false;
        var service = new ZoneReadyService();

        service.Validate(state, 1, 0);

        Assert.Equal(0, state.AutoTimeHack);
    }

    [Fact]
    public void PlayerNotYetTickedIntoZone_SkipsGuardsButStillMarksInWorld()
    {
        // No PlayerRuntimeState exists yet to hand the service -- the skip-guards-but-still-admit fallback is
        // handler-owned plumbing, not service business logic, so exercise the real handler here.
        var (session, _) = ZoneTestKit.CreateSession(10);
        session.MarkTicketConsumed(1, 10);
        session.MarkRegistering();
        // Deliberately never posted/ticked ZoneCommand.Enter, nor set CurrentZone -- the benign staleness
        // window this handler already tolerated before C04 (nothing to validate against yet).
        var handler = new ZoneReadyHandler(new ZoneReadyService());

        handler.Handle(new ZoneReadyRequest { Tribe = 999, AutoTime = 0, AutoTime2 = 0, AutoState = 1 }, session);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(ZoneSessionState.InWorld, session.State);
    }
}

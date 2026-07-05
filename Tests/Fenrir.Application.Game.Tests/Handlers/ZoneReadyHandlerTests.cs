using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class ZoneReadyHandlerTests
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

    private static ZoneReadyRequest Packet(int tribe = 1, int autoState = 0)
    {
        return new ZoneReadyRequest { Tribe = tribe, AutoTime = 0, AutoTime2 = 0, AutoState = autoState };
    }

    [Fact]
    public void ValidHandshake_MarksInWorldAndStampsConnectTime()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10, tribe: 2);
        var handler = new ZoneReadyHandler();

        handler.Handle(Packet(tribe: 2), session);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(ZoneSessionState.InWorld, session.State);
        Assert.NotNull(state.ConnectTime);
        Assert.Equal(0, state.AutoTimeHack);
    }

    [Fact]
    public void TribeMismatch_AbortsAndNeverMarksInWorld()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10, tribe: 1);
        var handler = new ZoneReadyHandler();

        // Client claims tribe 3 while the world-entry snapshot loaded tribe 1 -- a patched client.
        handler.Handle(Packet(tribe: 3), session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Equal(ZoneSessionState.Registering, session.State);
        Assert.Null(state.ConnectTime);
    }

    [Fact]
    public void StaleHeartbeat_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10, tribe: 1);
        state.LastSentHeartbeat = DateTime.UtcNow.AddSeconds(-11);
        var handler = new ZoneReadyHandler();

        handler.Handle(Packet(tribe: 1), session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Equal(ZoneSessionState.Registering, session.State);
    }

    [Fact]
    public void FreshHeartbeat_DoesNotAbort()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10, tribe: 1);
        state.LastSentHeartbeat = DateTime.UtcNow.AddSeconds(-1);
        var handler = new ZoneReadyHandler();

        handler.Handle(Packet(tribe: 1), session);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(ZoneSessionState.InWorld, session.State);
    }

    [Fact]
    public void NeverSentHeartbeat_SkipsWatchdogGuard()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10, tribe: 1);
        Assert.Null(state.LastSentHeartbeat); // never sent, matching legacy's mLastSentHeartbeat == -1
        var handler = new ZoneReadyHandler();

        handler.Handle(Packet(tribe: 1), session);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(ZoneSessionState.InWorld, session.State);
    }

    [Fact]
    public void AutoStateClaimedWithoutServerAutoHunt_AbortsOnThirdStrike()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10, tribe: 1);
        state.AutoHuntEnabled = false;
        var handler = new ZoneReadyHandler();

        handler.Handle(Packet(tribe: 1, autoState: 1), session);
        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, state.AutoTimeHack);

        handler.Handle(Packet(tribe: 1, autoState: 1), session);
        Assert.Null(session.DisconnectReason);
        Assert.Equal(2, state.AutoTimeHack);

        handler.Handle(Packet(tribe: 1, autoState: 1), session);
        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Equal(3, state.AutoTimeHack);
    }

    [Fact]
    public void AutoStateClaimedWithServerAutoHuntEnabled_NeverStrikes()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10, tribe: 1);
        state.AutoHuntEnabled = true;
        var handler = new ZoneReadyHandler();

        handler.Handle(Packet(tribe: 1, autoState: 1), session);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, state.AutoTimeHack);
        Assert.Equal(ZoneSessionState.InWorld, session.State);
    }

    [Fact]
    public void AutoStateZero_NeverStrikesRegardlessOfServerAutoHunt()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10, tribe: 1);
        state.AutoHuntEnabled = false;
        var handler = new ZoneReadyHandler();

        handler.Handle(Packet(tribe: 1, autoState: 0), session);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, state.AutoTimeHack);
    }

    [Fact]
    public void PlayerNotYetTickedIntoZone_SkipsGuardsButStillMarksInWorld()
    {
        var (session, _) = ZoneTestKit.CreateSession(10);
        session.MarkTicketConsumed(1, 10);
        session.MarkRegistering();
        // Deliberately never posted/ticked ZoneCommand.Enter, nor set CurrentZone -- the benign staleness
        // window this handler already tolerated before C04 (nothing to validate against yet).
        var handler = new ZoneReadyHandler();

        handler.Handle(Packet(tribe: 999, autoState: 1), session);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(ZoneSessionState.InWorld, session.State);
    }
}

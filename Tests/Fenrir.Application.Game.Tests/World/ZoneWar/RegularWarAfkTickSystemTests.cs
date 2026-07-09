using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="RegularWarAfkTickSystem" /> (<c>USE_REGULAR_WAR_AFK_TICK</c>) -- the per-avatar
///     <c>mAFKTick</c> accrual, the interim warning cadence, the forced-return-to-auto-zone notice, and the
///     hard disconnect, for both the Zone049-active-battle gate and the Zone195 "at all" gate. Every test
///     drives the system directly via <see cref="RegularWarAfkTickSystem.Simulate" /> rather than through
///     <see cref="Zone.Tick" />'s full pipeline, matching <c>AntiCampingForcedReturnSystemTests</c>'s own
///     posture.
/// </summary>
public class RegularWarAfkTickSystemTests
{
    // A real RegularWarMapCatalog-configured map (server 49, WarSlotIndex 0) -- see RegularWarMapCatalog.
    private const short RegularWarMapId = 49;

    // Neither Zone195-configured nor a RegularWarMapCatalog entry -- the cheapest no-op branch.
    private const short OrdinaryMapId = 999;

    // Configured only via GameServerOptions.Zone195MapIds in the tests that need it -- deliberately NOT a
    // RegularWarMapCatalog entry, to prove Zone195 enforcement doesn't depend on that catalog at all.
    private const short Zone195MapId = 500;

    private static (Zone Zone, PlayerRuntimeState State, ZoneClientSession Session, FakeDuplexPipe Pipe) EnterPlayer(
        short mapId, int characterId = 10)
    {
        var zone = ZoneTestKit.CreateZone(mapId);
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // just drains the Enter command
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        ZoneTestKit.DrainOutbound(pipe);
        return (zone, state!, session, pipe);
    }

    private static RegularWarAfkTickSystem CreateSystem(RegularWarActiveMapTracker? tracker = null,
        GameServerOptions? options = null, ILogger<RegularWarAfkTickSystem>? logger = null)
    {
        return new RegularWarAfkTickSystem(tracker ?? new RegularWarActiveMapTracker(),
            Options.Create(options ?? new GameServerOptions()), logger ?? NullLogger<RegularWarAfkTickSystem>.Instance);
    }

    private static byte[] ReturnToHomeZoneFrame()
    {
        var frame = new byte[FrameWriter.FrameSizeOf<ReturnToHomeZoneResponse>()];
        FrameWriter.WriteFrame(new ReturnToHomeZoneResponse(), frame);
        return frame;
    }

    [Fact]
    public void OrdinaryMap_NeverTouchesTheCounter_EvenIfAlreadyNonZero()
    {
        var (zone, state, _, pipe) = EnterPlayer(OrdinaryMapId);
        state.AfkTick = 5; // a stale value that must survive untouched on a completely unrelated map
        var system = CreateSystem();

        system.Simulate(zone, 1);

        Assert.Equal(5, state.AfkTick);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void RegularWarMap_NotCurrentlyActive_ResetsStaleCounterToZero()
    {
        var (zone, state, _, _) = EnterPlayer(RegularWarMapId);
        state.AfkTick = 42;
        var tracker = new RegularWarActiveMapTracker(); // never reported a phase -> IsBattleInProgress == false
        var system = CreateSystem(tracker);

        system.Simulate(zone, 1);

        Assert.Equal(0, state.AfkTick);
    }

    [Fact]
    public void RegularWarMap_ActiveBattle_IncrementsCounterEachSimulateCall()
    {
        var (zone, state, _, _) = EnterPlayer(RegularWarMapId);
        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(RegularWarMapId, RegularWarPhase.Active);
        var system = CreateSystem(tracker);

        system.Simulate(zone, 1);
        Assert.Equal(1, state.AfkTick);

        system.Simulate(zone, 1);
        Assert.Equal(2, state.AfkTick);
    }

    [Fact]
    public void RegularWarMap_ActiveBattle_MovingZonePlayer_NeverAccrues()
    {
        var (zone, state, _, pipe) = EnterPlayer(RegularWarMapId);
        state.IsMovingZone = true;
        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(RegularWarMapId, RegularWarPhase.Active);
        var system = CreateSystem(tracker);

        var fullTicks = RegularWarAfkTickSystem.WarActiveFullUnits * RegularWarAfkTickSystem.UnitLegacyTicks;
        system.Simulate(zone, fullTicks + RegularWarAfkTickSystem.DisconnectGraceLegacyTicksPastFull + 10);

        Assert.Equal(0, state.AfkTick);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void RegularWarMap_ActiveBattle_InterimWarning_LoggedAtEachUnitBoundaryBeforeFull_NotAfter()
    {
        var (zone, _, _, _) = EnterPlayer(RegularWarMapId);
        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(RegularWarMapId, RegularWarPhase.Active);
        var logger = new CapturingLogger<RegularWarAfkTickSystem>();
        var system = CreateSystem(tracker, logger: logger);

        // WarActiveFullUnits=5, UnitLegacyTicks=60 -> full at 300. Advance one unit (60 ticks) at a time.
        for (var unit = 1; unit <= RegularWarAfkTickSystem.WarActiveFullUnits; unit++)
        {
            logger.Entries.Clear();
            system.Simulate(zone, RegularWarAfkTickSystem.UnitLegacyTicks);

            if (unit < RegularWarAfkTickSystem.WarActiveFullUnits)
                Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information);
            else
                // The tick that reaches the full threshold itself is handled by the forced-return branch,
                // not the interim warning -- no additional info log fires on this exact crossing.
                Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Information);
        }
    }

    [Fact]
    public void RegularWarMap_ActiveBattle_ReachesFullThreshold_SendsReturnToHomeZoneExactlyOnce()
    {
        var (zone, state, _, pipe) = EnterPlayer(RegularWarMapId);
        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(RegularWarMapId, RegularWarPhase.Active);
        var system = CreateSystem(tracker);

        var fullTicks = RegularWarAfkTickSystem.WarActiveFullUnits * RegularWarAfkTickSystem.UnitLegacyTicks;

        system.Simulate(zone, fullTicks - 1);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));

        system.Simulate(zone, 1); // crosses the threshold on this exact tick
        Assert.Equal(fullTicks, state.AfkTick);
        Assert.Equal(ReturnToHomeZoneFrame(), ZoneTestKit.DrainOutbound(pipe));

        system.Simulate(zone, 1); // one tick further, still short of the disconnect grace -- no repeat notice
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void RegularWarMap_ActiveBattle_PastDisconnectGrace_Disconnects()
    {
        var (zone, _, session, _) = EnterPlayer(RegularWarMapId);
        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(RegularWarMapId, RegularWarPhase.Active);
        var system = CreateSystem(tracker);

        var fullTicks = RegularWarAfkTickSystem.WarActiveFullUnits * RegularWarAfkTickSystem.UnitLegacyTicks;

        system.Simulate(zone, fullTicks + RegularWarAfkTickSystem.DisconnectGraceLegacyTicksPastFull - 1);
        Assert.Null(session.DisconnectReason);

        system.Simulate(zone, 1); // the exact fullTicks+2 mark
        Assert.Equal(DisconnectReason.IdleTimeout, session.DisconnectReason);
    }

    [Fact]
    public void RegularWarMap_ActiveBattle_BurstOfManyTicks_ReachesFullAndDisconnectsInOneCall()
    {
        var (zone, state, session, pipe) = EnterPlayer(RegularWarMapId);
        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(RegularWarMapId, RegularWarPhase.Active);
        var system = CreateSystem(tracker);

        var fullTicks = RegularWarAfkTickSystem.WarActiveFullUnits * RegularWarAfkTickSystem.UnitLegacyTicks;
        system.Simulate(zone, fullTicks + RegularWarAfkTickSystem.DisconnectGraceLegacyTicksPastFull);

        Assert.Equal(fullTicks + RegularWarAfkTickSystem.DisconnectGraceLegacyTicksPastFull, state.AfkTick);
        Assert.Equal(ReturnToHomeZoneFrame(), ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(DisconnectReason.IdleTimeout, session.DisconnectReason);
    }

    [Fact]
    public void Zone195Map_EnforcedRegardlessOfRegularWarCatalogOrTrackerState()
    {
        var options = new GameServerOptions { Zone195MapIds = new HashSet<short> { Zone195MapId } };
        var (zone, state, _, pipe) = EnterPlayer(Zone195MapId);
        var system = CreateSystem(options: options); // default tracker never reports anything for this map

        var fullTicks = RegularWarAfkTickSystem.Zone195FullUnits * RegularWarAfkTickSystem.UnitLegacyTicks;
        system.Simulate(zone, fullTicks);

        Assert.Equal(fullTicks, state.AfkTick);
        Assert.Equal(ReturnToHomeZoneFrame(), ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Zone195Map_NotConfigured_TreatedAsOrdinaryMap()
    {
        // Same map id as the Zone195 test above, but GameServerOptions.Zone195MapIds is empty and it is not a
        // RegularWarMapCatalog entry either -- must fall into the cheapest no-op branch.
        var (zone, state, _, pipe) = EnterPlayer(Zone195MapId);
        state.AfkTick = 9;
        var system = CreateSystem();

        system.Simulate(zone, RegularWarAfkTickSystem.Zone195FullUnits * RegularWarAfkTickSystem.UnitLegacyTicks);

        Assert.Equal(9, state.AfkTick); // untouched, not even reset
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void RegularWarMap_BattleEndsBetweenTicks_CounterResetsInsteadOfContinuingToAccrue()
    {
        var (zone, state, _, _) = EnterPlayer(RegularWarMapId);
        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(RegularWarMapId, RegularWarPhase.Active);
        var system = CreateSystem(tracker);

        system.Simulate(zone, 30);
        Assert.Equal(30, state.AfkTick);

        tracker.ReportPhase(RegularWarMapId, RegularWarPhase.PostWarCleanup); // battle no longer active
        system.Simulate(zone, 1);

        Assert.Equal(0, state.AfkTick);
    }
}

using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Fenrir.Network.Framing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class Zone335FfaEventCycleSystemTests
{
    private const short FfaMapId = 335;
    private const short OtherMapId = 1;

    private static (Zone335FfaEventCycleSystem System, ZoneCenterSiegeState State, Zone335StartTrigger Trigger,
        ZoneRegistry Registry) CreateSystem()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([FfaMapId]);

        var state = new ZoneCenterSiegeState();
        var trigger = new Zone335StartTrigger();
        var broadcaster =
            new ZoneEventBroadcaster(ZoneTestKit.CreateWorldState(), registry,
                NullLogger<ZoneEventBroadcaster>.Instance, siegeState: state);
        var system = new Zone335FfaEventCycleSystem(state, trigger, new Lazy<ZoneEventBroadcaster>(() => broadcaster),
            NullLogger<Zone335FfaEventCycleSystem>.Instance);

        return (system, state, trigger, registry);
    }

    [Fact]
    public void Simulate_OnAnyOtherZone_IsANoOp()
    {
        var (system, state, _, registry) = CreateSystem();
        registry.Initialize([FfaMapId, OtherMapId]);

        system.Simulate(registry[OtherMapId], 100_000);

        Assert.Equal(Zone335FfaPhase.Idle, system.Phase);
        Assert.Equal(0, state.Zone335);
    }

    [Fact]
    public void Idle_GmStartRequest_SkipsTheSixtyMinuteWait_EntersCountdownArmedImmediately()
    {
        var (system, _, trigger, registry) = CreateSystem();
        trigger.Request(600); // whatever countdown the GM command computed -- deliberately irrelevant, see remarks

        system.Simulate(registry[FfaMapId], 1);

        Assert.Equal(Zone335FfaPhase.CountdownArmed, system.Phase);
        Assert.False(trigger.StartRequested); // consumed
    }

    [Fact]
    public void Idle_NoTrigger_DoesNotAdvance_UntilSixtyInGameMinutesAccumulate()
    {
        var (system, _, _, registry) = CreateSystem();
        var zone = registry[FfaMapId];

        system.Simulate(zone, Zone335FfaEventCycleSystem.IdleAutoStartLegacyTicks - 1);
        Assert.Equal(Zone335FfaPhase.Idle, system.Phase);

        system.Simulate(zone, 1);
        Assert.Equal(Zone335FfaPhase.CountdownArmed, system.Phase);
    }

    [Fact]
    public void Idle_OnAdvance_ResetsTheFourTribeBonusFields()
    {
        var (system, state, trigger, registry) = CreateSystem();
        state.SetExperienceBonusRatio(0, 1.5f);
        state.SetItemDropBonusRatio(1, 2.5f);
        state.SetMyoungItemDropBonusRatio(2, 3.5f);
        state.SetKillOtherTribeBonus(3, 10);
        trigger.Request(0);

        system.Simulate(registry[FfaMapId], 1);

        Assert.Equal(0f, state.GetExperienceBonusRatio(0));
        Assert.Equal(0f, state.GetItemDropBonusRatio(1));
        Assert.Equal(0f, state.GetMyoungItemDropBonusRatio(2));
        Assert.Equal(0, state.GetKillOtherTribeBonus(3));
    }

    /// <summary>Drives the whole cycle Idle -> ... -> WindDown -> Idle with nobody online, asserting every
    /// client-visible <see cref="ZoneCenterSiegeState.Zone335"/> value the source contract's own 1501-1507
    /// table specifies is hit in order, and the machine returns to a clean idle state.</summary>
    [Fact]
    public void FullCycle_WithNoPlayersOnline_VisitsEveryPhaseScalarInOrder_AndReturnsToIdle()
    {
        var (system, state, trigger, registry) = CreateSystem();
        var zone = registry[FfaMapId];
        trigger.Request(0);

        // Idle -> CountdownArmed (GM-skip).
        system.Simulate(zone, 1);
        Assert.Equal(Zone335FfaPhase.CountdownArmed, system.Phase);
        Assert.Equal(0, state.Zone335);

        // CountdownArmed -> PreStartCountdown (single tick).
        system.Simulate(zone, 1);
        Assert.Equal(Zone335FfaPhase.PreStartCountdown, system.Phase);

        // PreStartCountdown: 10 minutes of countdown broadcasts, then 1 more minute to advance.
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PreStartCountdownMinutes + 1);
        Assert.Equal(Zone335FfaPhase.GateOpenPending, system.Phase);
        Assert.Equal(0, state.Zone335);

        // GateOpenPending -> EntranceOpen (1 minute), scalar 0 -> 1.
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.GateOpenWaitMinutes);
        Assert.Equal(Zone335FfaPhase.EntranceOpen, system.Phase);
        Assert.Equal(1, state.Zone335);

        // EntranceOpen -> BattlePrep (2 minutes), scalar 1 -> 2.
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.EntranceOpenWindowMinutes);
        Assert.Equal(Zone335FfaPhase.BattlePrep, system.Phase);
        Assert.Equal(2, state.Zone335);

        // BattlePrep -> Battle (1 minute), scalar 2 -> 3.
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.BattlePrepWaitMinutes);
        Assert.Equal(Zone335FfaPhase.Battle, system.Phase);
        Assert.Equal(3, state.Zone335);

        // Battle: nobody online, so last-man-standing (eligibleCount <= 1) is already true, but the
        // more-than-one-minute opening guard must elapse first.
        system.Simulate(zone, Zone335FfaEventCycleSystem.BattleMinimumElapsedLegacyTicksForLastManStanding);
        Assert.Equal(Zone335FfaPhase.Battle, system.Phase); // guard not yet past (strict >)

        system.Simulate(zone, 1);
        Assert.Equal(Zone335FfaPhase.PostBattleCleanupPending, system.Phase);
        Assert.Equal(4, state.Zone335);

        // PostBattleCleanupPending -> WindDown (1 minute), scalar 4 -> 5.
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PostBattleCleanupWaitMinutes);
        Assert.Equal(Zone335FfaPhase.WindDown, system.Phase);
        Assert.Equal(5, state.Zone335);

        // WindDown -> Idle (1 minute), scalar 5 -> 0.
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.WindDownWaitMinutes);
        Assert.Equal(Zone335FfaPhase.Idle, system.Phase);
        Assert.Equal(0, state.Zone335);
    }

    [Fact]
    public void Battle_TimerExpiry_EndsTheBattle_EvenWithoutLastManStanding()
    {
        var (system, state, trigger, registry) = CreateSystem();
        var zone = registry[FfaMapId];
        trigger.Request(0);

        system.Simulate(zone, 1); // -> CountdownArmed
        system.Simulate(zone, 1); // -> PreStartCountdown
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PreStartCountdownMinutes + 1); // -> GateOpenPending
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.GateOpenWaitMinutes); // -> EntranceOpen
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.EntranceOpenWindowMinutes); // -> BattlePrep
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.BattlePrepWaitMinutes); // -> Battle
        Assert.Equal(Zone335FfaPhase.Battle, system.Phase);

        system.Simulate(zone, Zone335FfaEventCycleSystem.BattleDurationLegacyTicks);

        Assert.Equal(Zone335FfaPhase.PostBattleCleanupPending, system.Phase);
        Assert.Equal(4, state.Zone335);
    }

    [Fact]
    public void Battle_LiveCountdownBroadcast_SentToZone335sOwnRosterOnly_EveryTenTicks()
    {
        var (system, _, trigger, registry) = CreateSystem();
        registry.Initialize([FfaMapId, OtherMapId]);
        var zone = registry[FfaMapId];
        var otherZone = registry[OtherMapId];
        trigger.Request(0);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(session, FfaMapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var (otherSession, otherPipe) = ZoneTestKit.CreateSession(2);
        otherZone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(otherSession, OtherMapId)));
        otherZone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(otherPipe);

        system.Simulate(zone, 1); // -> CountdownArmed
        system.Simulate(zone, 1); // -> PreStartCountdown
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PreStartCountdownMinutes + 1); // -> GateOpenPending
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.GateOpenWaitMinutes); // -> EntranceOpen
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.EntranceOpenWindowMinutes); // -> BattlePrep
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.BattlePrepWaitMinutes); // -> Battle

        // Discard every cluster-wide phase-transition broadcast up to here (these DO reach every zone,
        // including the other one -- only the LIVE countdown below is local-only, see the system's own remarks).
        DrainAll(pipe);
        DrainAll(otherPipe);

        system.Simulate(zone, Zone335FfaEventCycleSystem.LiveCountdownBroadcastCadenceLegacyTicks);

        var frame = DrainAll(pipe);
        var expectedSize = FrameWriter.FrameSizeOf<ZoneWar335CountdownResponse>();
        Assert.Equal(expectedSize, frame.Length);

        // The other zone's own player never receives the local-only live countdown.
        Assert.Empty(DrainAll(otherPipe));
    }

    /// <summary>
    ///     Drives the GM-skipped countdown one simulated minute (one <see cref="Zone335FfaEventCycleSystem.Simulate" />
    ///     call) at a time and asserts, at every single one of those successive calls, both that the phase has NOT
    ///     jumped ahead of schedule and that the exact contract-mandated sort-1501 payload (remaining whole
    ///     minutes, counting down 10..1) reaches a connected player -- closing the gap the other tests in this
    ///     class leave open by only checking <see cref="Zone335FfaEventCycleSystem.Phase" />/<see cref="ZoneCenterSiegeState.Zone335" />,
    ///     never the actual wire broadcast a real client would receive.
    /// </summary>
    [Fact]
    public void PreStartCountdown_AdvancesOneMinuteAtATime_BroadcastingSort1501WithDecreasingRemainingMinutes_ThenExitsWithNoFurtherBroadcast()
    {
        var (system, _, trigger, registry) = CreateSystem();
        var zone = registry[FfaMapId];
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(session, FfaMapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        DrainAll(pipe);

        trigger.Request(0);
        system.Simulate(zone, 1); // -> CountdownArmed
        system.Simulate(zone, 1); // -> PreStartCountdown
        Assert.Equal(Zone335FfaPhase.PreStartCountdown, system.Phase);
        Assert.Empty(DrainAll(pipe)); // neither of these two ticks broadcasts anything

        for (var minute = 1; minute <= Zone335FfaEventCycleSystem.PreStartCountdownMinutes; minute++)
        {
            system.Simulate(zone, SimulationClock.PlayTimeAccrualLegacyTicks);

            Assert.Equal(Zone335FfaPhase.PreStartCountdown, system.Phase); // never jumps ahead of its own schedule

            var frame = DrainAll(pipe);
            Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), frame.Length);
            var payload = frame.AsSpan(1);
            Assert.Equal(1501, BinaryPrimitives.ReadInt32LittleEndian(payload));
            var expectedRemaining = Zone335FfaEventCycleSystem.PreStartCountdownMinutes - minute + 1;
            Assert.Equal(expectedRemaining, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        }

        // The 11th minute exits the phase -- no further sort-1501 (or any) broadcast fires on this transition.
        system.Simulate(zone, SimulationClock.PlayTimeAccrualLegacyTicks);
        Assert.Equal(Zone335FfaPhase.GateOpenPending, system.Phase);
        Assert.Empty(DrainAll(pipe));
    }

    /// <summary>
    ///     End-to-end with a real connected, non-transferring player: verifies every one of the six contract
    ///     event codes (1502-1507) actually reaches the wire with the right sort/payload at the right phase
    ///     transition -- not merely that <see cref="ZoneCenterSiegeState.Zone335" /> changed internally -- and
    ///     that the terminal WindDown -&gt; Idle transition both force-returns this still-present player home and
    ///     broadcasts the 1507 reset to them, in that order.
    /// </summary>
    [Fact]
    public void FullCycle_WithOnePlayerOnline_BroadcastsEveryContractEventCodeInOrder_ThenForcesThemHomeAndResetsOnFinalWindDown()
    {
        var (system, state, trigger, registry) = CreateSystem();
        var zone = registry[FfaMapId];
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(session, FfaMapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        DrainAll(pipe);

        trigger.Request(0);
        system.Simulate(zone, 1); // -> CountdownArmed
        system.Simulate(zone, 1); // -> PreStartCountdown
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PreStartCountdownMinutes + 1); // -> GateOpenPending
        Assert.Equal(Zone335FfaPhase.GateOpenPending, system.Phase);
        DrainAll(pipe); // the ten sort-1501 countdown broadcasts are covered by the dedicated test above

        // GateOpenPending -> EntranceOpen: bare sort 1502.
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.GateOpenWaitMinutes);
        Assert.Equal(Zone335FfaPhase.EntranceOpen, system.Phase);
        AssertBareSortBroadcast(DrainAll(pipe), 1502);

        // EntranceOpen -> BattlePrep: bare sort 1503.
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.EntranceOpenWindowMinutes);
        Assert.Equal(Zone335FfaPhase.BattlePrep, system.Phase);
        AssertBareSortBroadcast(DrainAll(pipe), 1503);

        // BattlePrep -> Battle: sort 1504, payload = the fixed 1800-tick battle timer (never the GM's own
        // discarded duration parameter -- see Zone335StartTrigger.ConsumeStartRequest's own remarks).
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.BattlePrepWaitMinutes);
        Assert.Equal(Zone335FfaPhase.Battle, system.Phase);
        var battleStartFrame = DrainAll(pipe);
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), battleStartFrame.Length);
        Assert.Equal(1504, BinaryPrimitives.ReadInt32LittleEndian(battleStartFrame.AsSpan(1)));
        Assert.Equal(Zone335FfaEventCycleSystem.BattleDurationLegacyTicks,
            BinaryPrimitives.ReadInt32LittleEndian(battleStartFrame.AsSpan(5)));

        // Battle -> PostBattleCleanupPending: sort 1505, once past the opening guard with nobody else eligible.
        // The same Simulate call also necessarily crosses the much shorter live-countdown cadence, so the
        // sort-1505 frame is extracted rather than assumed to be the only frame in this batch.
        system.Simulate(zone, Zone335FfaEventCycleSystem.BattleMinimumElapsedLegacyTicksForLastManStanding + 1);
        Assert.Equal(Zone335FfaPhase.PostBattleCleanupPending, system.Phase);
        Assert.Equal(4, state.Zone335);
        var battleEndPayload = ExtractEventPayload(DrainAll(pipe), 1505);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(battleEndPayload[4..]));

        // PostBattleCleanupPending -> WindDown: bare sort 1506.
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PostBattleCleanupWaitMinutes);
        Assert.Equal(Zone335FfaPhase.WindDown, system.Phase);
        AssertBareSortBroadcast(DrainAll(pipe), 1506);

        // WindDown -> Idle: this player was never marked mid-transfer, so the final reset forces them home
        // (ReturnToHomeZoneResponse) BEFORE broadcasting the cluster-wide sort-1507 reset notice to them.
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.WindDownWaitMinutes);
        Assert.Equal(Zone335FfaPhase.Idle, system.Phase);
        Assert.Equal(0, state.Zone335);

        var resetBytes = DrainAll(pipe);
        var expectedReturnFrame = new byte[FrameWriter.FrameSizeOf<ReturnToHomeZoneResponse>()];
        FrameWriter.WriteFrame(new ReturnToHomeZoneResponse(), expectedReturnFrame);
        Assert.Equal(expectedReturnFrame, resetBytes[..expectedReturnFrame.Length]);

        var resetSort = BinaryPrimitives.ReadInt32LittleEndian(resetBytes.AsSpan(expectedReturnFrame.Length + 1));
        Assert.Equal(1507, resetSort);
    }

    /// <summary>
    ///     The final reset's <c>ForceReturnEligiblePlayers</c> step only skips a player already mid-transfer
    ///     (<see cref="PlayerRuntimeState.IsMovingZone" />) -- it does NOT exempt them from the cluster-wide
    ///     sort-1507 reset broadcast itself, since that fan-out is unconditional over every connected player
    ///     (<see cref="ZoneEventBroadcaster" />'s own <c>BroadcastToEveryZone</c>, not scoped by eligibility).
    /// </summary>
    [Fact]
    public void FinalReset_SkipsForcedReturnForAPlayerAlreadyMovingZone_ButStillBroadcastsTheClusterWideResetToThem()
    {
        var (system, state, trigger, registry) = CreateSystem();
        var zone = registry[FfaMapId];

        var (stayingSession, stayingPipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(stayingSession, FfaMapId)));
        var (transferringSession, transferringPipe) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(transferringSession, FfaMapId, name: "Mover")));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        DrainAll(stayingPipe);
        DrainAll(transferringPipe);

        Assert.True(zone.TryGetPlayer(2, out var transferringState));
        transferringState!.IsMovingZone = true;

        trigger.Request(0);
        system.Simulate(zone, 1); // -> CountdownArmed
        system.Simulate(zone, 1); // -> PreStartCountdown
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PreStartCountdownMinutes + 1); // -> GateOpenPending
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.GateOpenWaitMinutes); // -> EntranceOpen
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.EntranceOpenWindowMinutes); // -> BattlePrep
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.BattlePrepWaitMinutes); // -> Battle

        // The transferring player never counts toward eligibility, so last-man-standing is already true for
        // the staying player alone the instant the opening guard elapses.
        system.Simulate(zone, Zone335FfaEventCycleSystem.BattleMinimumElapsedLegacyTicksForLastManStanding + 1);
        Assert.Equal(Zone335FfaPhase.PostBattleCleanupPending, system.Phase);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PostBattleCleanupWaitMinutes); // -> WindDown
        Assert.Equal(Zone335FfaPhase.WindDown, system.Phase);
        DrainAll(stayingPipe);
        DrainAll(transferringPipe);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.WindDownWaitMinutes); // -> Idle, final reset
        Assert.Equal(Zone335FfaPhase.Idle, system.Phase);
        Assert.Equal(0, state.Zone335);

        var expectedReturnFrame = new byte[FrameWriter.FrameSizeOf<ReturnToHomeZoneResponse>()];
        FrameWriter.WriteFrame(new ReturnToHomeZoneResponse(), expectedReturnFrame);

        var stayingBytes = DrainAll(stayingPipe);
        Assert.Equal(expectedReturnFrame, stayingBytes[..expectedReturnFrame.Length]);
        Assert.Equal(1507,
            BinaryPrimitives.ReadInt32LittleEndian(stayingBytes.AsSpan(expectedReturnFrame.Length + 1)));

        // The transferring player receives ONLY the reset broadcast -- no ReturnToHomeZoneResponse at all.
        var transferringBytes = DrainAll(transferringPipe);
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), transferringBytes.Length);
        Assert.Equal(1507, BinaryPrimitives.ReadInt32LittleEndian(transferringBytes.AsSpan(1)));
    }

    private static void AssertBareSortBroadcast(byte[] frame, int expectedSort)
    {
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), frame.Length);
        var payload = frame.AsSpan(1);
        Assert.Equal(expectedSort, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[4..])); // no fields beyond the sort itself
    }

    /// <summary>
    ///     Scans a drained batch that may contain an unpredictable interleaving of <see cref="ZoneEventInfoResponse" />
    ///     (this system's own phase-transition broadcasts) and <see cref="ZoneWar335CountdownResponse" /> (the
    ///     separate, much-higher-cadence in-battle live countdown, which can legitimately land in the very same
    ///     <see cref="Zone335FfaEventCycleSystem.Simulate" /> call once the opening guard is crossed) frames, and
    ///     returns the payload (Sort + Data) of the one <see cref="ZoneEventInfoResponse" /> frame carrying
    ///     <paramref name="expectedSort" />.
    /// </summary>
    private static ReadOnlySpan<byte> ExtractEventPayload(byte[] bytes, int expectedSort)
    {
        var eventFrameSize = FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();
        var countdownFrameSize = FrameWriter.FrameSizeOf<ZoneWar335CountdownResponse>();
        var offset = 0;

        while (offset < bytes.Length)
        {
            if (bytes[offset] == ZoneEventInfoResponse.Opcode)
            {
                var payload = bytes.AsSpan(offset + 1, eventFrameSize - 1);
                if (BinaryPrimitives.ReadInt32LittleEndian(payload) == expectedSort)
                    return payload;

                offset += eventFrameSize;
            }
            else
            {
                offset += countdownFrameSize;
            }
        }

        throw new InvalidOperationException($"No ZoneEventInfoResponse frame with sort {expectedSort} was found.");
    }

    private static void AdvanceMinutes(Zone335FfaEventCycleSystem system, Zone zone, int minutes)
    {
        for (var i = 0; i < minutes; i++)
            system.Simulate(zone, SimulationClock.PlayTimeAccrualLegacyTicks);
    }

    /// <summary>
    ///     Repeatedly drains until a read returns nothing new -- a single <see cref="ZoneTestKit.DrainOutbound" />
    ///     call can observe only a prefix of everything written so far when several <c>ClientSession.Send</c>
    ///     calls landed close together (its own internal per-session flush/backpressure queue, see that class's
    ///     remarks), which matters here since this test deliberately fires many broadcasts back-to-back before
    ///     ever draining.
    /// </summary>
    private static byte[] DrainAll(FakeDuplexPipe pipe)
    {
        var all = new List<byte>();
        for (var i = 0; i < 64; i++)
        {
            var chunk = ZoneTestKit.DrainOutbound(pipe);
            if (chunk.Length == 0)
                break;

            all.AddRange(chunk);
        }

        return all.ToArray();
    }
}

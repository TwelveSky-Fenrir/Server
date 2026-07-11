using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
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
        trigger.Request(600);

        system.Simulate(registry[FfaMapId], 1);

        Assert.Equal(Zone335FfaPhase.CountdownArmed, system.Phase);
        Assert.False(trigger.StartRequested);
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

        [Fact]
    public void FullCycle_WithNoPlayersOnline_VisitsEveryPhaseScalarInOrder_AndReturnsToIdle()
    {
        var (system, state, trigger, registry) = CreateSystem();
        var zone = registry[FfaMapId];
        trigger.Request(0);

        system.Simulate(zone, 1);
        Assert.Equal(Zone335FfaPhase.CountdownArmed, system.Phase);
        Assert.Equal(0, state.Zone335);

        system.Simulate(zone, 1);
        Assert.Equal(Zone335FfaPhase.PreStartCountdown, system.Phase);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PreStartCountdownMinutes + 1);
        Assert.Equal(Zone335FfaPhase.GateOpenPending, system.Phase);
        Assert.Equal(0, state.Zone335);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.GateOpenWaitMinutes);
        Assert.Equal(Zone335FfaPhase.EntranceOpen, system.Phase);
        Assert.Equal(1, state.Zone335);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.EntranceOpenWindowMinutes);
        Assert.Equal(Zone335FfaPhase.BattlePrep, system.Phase);
        Assert.Equal(2, state.Zone335);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.BattlePrepWaitMinutes);
        Assert.Equal(Zone335FfaPhase.Battle, system.Phase);
        Assert.Equal(3, state.Zone335);

        system.Simulate(zone, Zone335FfaEventCycleSystem.BattleMinimumElapsedLegacyTicksForLastManStanding);
        Assert.Equal(Zone335FfaPhase.Battle, system.Phase);

        system.Simulate(zone, 1);
        Assert.Equal(Zone335FfaPhase.PostBattleCleanupPending, system.Phase);
        Assert.Equal(4, state.Zone335);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PostBattleCleanupWaitMinutes);
        Assert.Equal(Zone335FfaPhase.WindDown, system.Phase);
        Assert.Equal(5, state.Zone335);

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

        system.Simulate(zone, 1);
        system.Simulate(zone, 1);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PreStartCountdownMinutes + 1);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.GateOpenWaitMinutes);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.EntranceOpenWindowMinutes);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.BattlePrepWaitMinutes);
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

        system.Simulate(zone, 1);
        system.Simulate(zone, 1);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PreStartCountdownMinutes + 1);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.GateOpenWaitMinutes);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.EntranceOpenWindowMinutes);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.BattlePrepWaitMinutes);

        DrainAll(pipe);
        DrainAll(otherPipe);

        system.Simulate(zone, Zone335FfaEventCycleSystem.LiveCountdownBroadcastCadenceLegacyTicks);

        var frame = DrainAll(pipe);
        var expectedSize = FrameWriter.FrameSizeOf<ZoneWar335CountdownResponse>();
        Assert.Equal(expectedSize, frame.Length);

        Assert.Empty(DrainAll(otherPipe));
    }

        [Fact]
    public void
        PreStartCountdown_AdvancesOneMinuteAtATime_BroadcastingSort1501WithDecreasingRemainingMinutes_ThenExitsWithNoFurtherBroadcast()
    {
        var (system, _, trigger, registry) = CreateSystem();
        var zone = registry[FfaMapId];
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(session, FfaMapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        DrainAll(pipe);

        trigger.Request(0);
        system.Simulate(zone, 1);
        system.Simulate(zone, 1);
        Assert.Equal(Zone335FfaPhase.PreStartCountdown, system.Phase);
        Assert.Empty(DrainAll(pipe));

        for (var minute = 1; minute <= Zone335FfaEventCycleSystem.PreStartCountdownMinutes; minute++)
        {
            system.Simulate(zone, SimulationClock.PlayTimeAccrualLegacyTicks);

            Assert.Equal(Zone335FfaPhase.PreStartCountdown, system.Phase);

            var frame = DrainAll(pipe);
            Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), frame.Length);
            var payload = frame.AsSpan(1);
            Assert.Equal(1501, BinaryPrimitives.ReadInt32LittleEndian(payload));
            var expectedRemaining = Zone335FfaEventCycleSystem.PreStartCountdownMinutes - minute + 1;
            Assert.Equal(expectedRemaining, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        }

        system.Simulate(zone, SimulationClock.PlayTimeAccrualLegacyTicks);
        Assert.Equal(Zone335FfaPhase.GateOpenPending, system.Phase);
        Assert.Empty(DrainAll(pipe));
    }

        [Fact]
    public void
        FullCycle_WithOnePlayerOnline_BroadcastsEveryContractEventCodeInOrder_ThenForcesThemHomeAndResetsOnFinalWindDown()
    {
        var (system, state, trigger, registry) = CreateSystem();
        var zone = registry[FfaMapId];
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(session, FfaMapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        DrainAll(pipe);

        trigger.Request(0);
        system.Simulate(zone, 1);
        system.Simulate(zone, 1);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PreStartCountdownMinutes + 1);
        Assert.Equal(Zone335FfaPhase.GateOpenPending, system.Phase);
        DrainAll(pipe);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.GateOpenWaitMinutes);
        Assert.Equal(Zone335FfaPhase.EntranceOpen, system.Phase);
        AssertBareSortBroadcast(DrainAll(pipe), 1502);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.EntranceOpenWindowMinutes);
        Assert.Equal(Zone335FfaPhase.BattlePrep, system.Phase);
        AssertBareSortBroadcast(DrainAll(pipe), 1503);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.BattlePrepWaitMinutes);
        Assert.Equal(Zone335FfaPhase.Battle, system.Phase);
        var battleStartFrame = DrainAll(pipe);
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), battleStartFrame.Length);
        Assert.Equal(1504, BinaryPrimitives.ReadInt32LittleEndian(battleStartFrame.AsSpan(1)));
        Assert.Equal(Zone335FfaEventCycleSystem.BattleDurationLegacyTicks,
            BinaryPrimitives.ReadInt32LittleEndian(battleStartFrame.AsSpan(5)));

        system.Simulate(zone, Zone335FfaEventCycleSystem.BattleMinimumElapsedLegacyTicksForLastManStanding + 1);
        Assert.Equal(Zone335FfaPhase.PostBattleCleanupPending, system.Phase);
        Assert.Equal(4, state.Zone335);
        var battleEndPayload = ExtractEventPayload(DrainAll(pipe), 1505);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(battleEndPayload[4..]));

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PostBattleCleanupWaitMinutes);
        Assert.Equal(Zone335FfaPhase.WindDown, system.Phase);
        AssertBareSortBroadcast(DrainAll(pipe), 1506);

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

        [Fact]
    public void FinalReset_SkipsForcedReturnForAPlayerAlreadyMovingZone_ButStillBroadcastsTheClusterWideResetToThem()
    {
        var (system, state, trigger, registry) = CreateSystem();
        var zone = registry[FfaMapId];

        var (stayingSession, stayingPipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(stayingSession, FfaMapId)));
        var (transferringSession, transferringPipe) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(transferringSession, FfaMapId, "Mover")));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        DrainAll(stayingPipe);
        DrainAll(transferringPipe);

        Assert.True(zone.TryGetPlayer(2, out var transferringState));
        transferringState!.IsMovingZone = true;

        trigger.Request(0);
        system.Simulate(zone, 1);
        system.Simulate(zone, 1);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PreStartCountdownMinutes + 1);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.GateOpenWaitMinutes);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.EntranceOpenWindowMinutes);
        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.BattlePrepWaitMinutes);

        system.Simulate(zone, Zone335FfaEventCycleSystem.BattleMinimumElapsedLegacyTicksForLastManStanding + 1);
        Assert.Equal(Zone335FfaPhase.PostBattleCleanupPending, system.Phase);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.PostBattleCleanupWaitMinutes);
        Assert.Equal(Zone335FfaPhase.WindDown, system.Phase);
        DrainAll(stayingPipe);
        DrainAll(transferringPipe);

        AdvanceMinutes(system, zone, Zone335FfaEventCycleSystem.WindDownWaitMinutes);
        Assert.Equal(Zone335FfaPhase.Idle, system.Phase);
        Assert.Equal(0, state.Zone335);

        var expectedReturnFrame = new byte[FrameWriter.FrameSizeOf<ReturnToHomeZoneResponse>()];
        FrameWriter.WriteFrame(new ReturnToHomeZoneResponse(), expectedReturnFrame);

        var stayingBytes = DrainAll(stayingPipe);
        Assert.Equal(expectedReturnFrame, stayingBytes[..expectedReturnFrame.Length]);
        Assert.Equal(1507,
            BinaryPrimitives.ReadInt32LittleEndian(stayingBytes.AsSpan(expectedReturnFrame.Length + 1)));

        var transferringBytes = DrainAll(transferringPipe);
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), transferringBytes.Length);
        Assert.Equal(1507, BinaryPrimitives.ReadInt32LittleEndian(transferringBytes.AsSpan(1)));
    }

    private static void AssertBareSortBroadcast(byte[] frame, int expectedSort)
    {
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), frame.Length);
        var payload = frame.AsSpan(1);
        Assert.Equal(expectedSort, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
    }

        private static ReadOnlySpan<byte> ExtractEventPayload(byte[] bytes, int expectedSort)
    {
        var eventFrameSize = FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();
        var countdownFrameSize = FrameWriter.FrameSizeOf<ZoneWar335CountdownResponse>();
        var offset = 0;

        while (offset < bytes.Length)
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

        throw new InvalidOperationException($"No ZoneEventInfoResponse frame with sort {expectedSort} was found.");
    }

    private static void AdvanceMinutes(Zone335FfaEventCycleSystem system, Zone zone, int minutes)
    {
        for (var i = 0; i < minutes; i++)
            system.Simulate(zone, SimulationClock.PlayTimeAccrualLegacyTicks);
    }

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

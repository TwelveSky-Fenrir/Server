using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="WorldClockPushSystem" />: the self-throttled per-minute "current time" push (A6-
///     sendtime-push contract) -- the rearm/blocked/just-sent state machine, the packed-time encoding, and
///     the forced (registration-path) send.
/// </summary>
public sealed class WorldClockPushSystemTests
{
    private static byte[] ExpectedFrame(AvatarStatUpdateResponse packet)
    {
        var frameSize = FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();
        var buffer = new byte[frameSize];
        FrameWriter.WriteFrame(in packet, buffer);
        return buffer;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) EnterWorld(WorldClockPushSystem system)
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [system]);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe); // discard the world-entry handshake's own traffic

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!, pipe);
    }

    [Fact]
    public void EncodePackedTime_PacksMonthZeroBasedDayHourMinuteWeekday_WithNoSeparators()
    {
        // 2026-07-12 14:07:xx is a Sunday -- month is 0-based (July -> 6), weekday Sunday -> 0.
        var instant = new DateTimeOffset(2026, 7, 12, 14, 7, 30, TimeSpan.Zero);

        var packed = WorldClockPushSystem.EncodePackedTime(instant);

        // "06" + "12" + "14" + "07" + "0" concatenated = "061214070" = 61214070
        Assert.Equal(61_214_070, packed);
    }

    [Fact]
    public void Simulate_SecondBelowRearmThreshold_FromUnsetState_SendsImmediately()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 12, 14, 7, 1, TimeSpan.Zero));
        var system = new WorldClockPushSystem(time);
        var (zone, state, pipe) = EnterWorld(system);

        Assert.Equal(WorldClockPushSystem.ThrottleStateUnset, state.WorldClockPushThrottleState);

        zone.Tick(SimulationClock.LegacyTick);

        var expected = WorldClockPushSystem.BuildPacket(time.Now);
        Assert.Equal(ExpectedFrame(expected), ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(WorldClockPushSystem.ThrottleStateJustSent, state.WorldClockPushThrottleState);
    }

    [Fact]
    public void Simulate_SecondAtOrAboveBlockThreshold_FromUnsetState_DoesNotSend_AndMarksBlocked()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 12, 14, 7, 3, TimeSpan.Zero));
        var system = new WorldClockPushSystem(time);
        var (zone, state, pipe) = EnterWorld(system);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(WorldClockPushSystem.ThrottleStateBlocked, state.WorldClockPushThrottleState);
    }

    [Fact]
    public void Simulate_AlreadyJustSent_SameMinute_DoesNotReSend()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 12, 14, 7, 1, TimeSpan.Zero));
        var system = new WorldClockPushSystem(time);
        var (zone, state, pipe) = EnterWorld(system);

        zone.Tick(SimulationClock.LegacyTick); // sends, -> JustSent
        ZoneTestKit.DrainOutbound(pipe);

        time.Now = time.Now.AddSeconds(1); // still second 2 -- neither rearm nor block condition fires
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(WorldClockPushSystem.ThrottleStateJustSent, state.WorldClockPushThrottleState);
    }

    [Fact]
    public void Simulate_BlockedThenNextMinuteRearmWindow_SendsAgain()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 12, 14, 7, 3, TimeSpan.Zero));
        var system = new WorldClockPushSystem(time);
        var (zone, state, pipe) = EnterWorld(system);

        zone.Tick(SimulationClock.LegacyTick); // second=3 -> Blocked, no send
        ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(WorldClockPushSystem.ThrottleStateBlocked, state.WorldClockPushThrottleState);

        time.Now = new DateTimeOffset(2026, 7, 12, 14, 8, 0, TimeSpan.Zero); // next minute, second=0 -> rearm window
        zone.Tick(SimulationClock.LegacyTick);

        var expected = WorldClockPushSystem.BuildPacket(time.Now);
        Assert.Equal(ExpectedFrame(expected), ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(WorldClockPushSystem.ThrottleStateJustSent, state.WorldClockPushThrottleState);
    }

    [Fact]
    public void Simulate_MissedRearmWindow_StaysBlockedUntilFollowingMinute()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 12, 14, 7, 3, TimeSpan.Zero));
        var system = new WorldClockPushSystem(time);
        var (zone, state, pipe) = EnterWorld(system);

        zone.Tick(SimulationClock.LegacyTick); // Blocked
        ZoneTestKit.DrainOutbound(pipe);

        // Simulated stall: the next observed sample jumps straight past the [0,2) rearm window.
        time.Now = new DateTimeOffset(2026, 7, 12, 14, 8, 5, TimeSpan.Zero);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(WorldClockPushSystem.ThrottleStateBlocked, state.WorldClockPushThrottleState);
    }

    [Fact]
    public void SendForced_IgnoresThrottleState_AlwaysSendsAndMarksJustSent()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 12, 14, 7, 3, TimeSpan.Zero));
        var system = new WorldClockPushSystem(time);
        var (_, state, pipe) = EnterWorld(system);
        state.WorldClockPushThrottleState = WorldClockPushSystem.ThrottleStateBlocked;

        system.SendForced(state);

        var expected = WorldClockPushSystem.BuildPacket(time.Now);
        Assert.Equal(ExpectedFrame(expected), ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(WorldClockPushSystem.ThrottleStateJustSent, state.WorldClockPushThrottleState);
    }

    /// <summary>A <see cref="TimeProvider" /> whose <see cref="TimeProvider.GetUtcNow" /> returns a settable instant.</summary>
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }
}

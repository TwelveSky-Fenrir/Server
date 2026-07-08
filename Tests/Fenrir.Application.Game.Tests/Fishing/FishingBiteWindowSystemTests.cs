using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Fishing;

/// <summary>
///     Drives the real <see cref="FishingBiteWindowSystem" /> (CZ_FISHING_RESULT_SEND's server-driven,
///     non-client-initiated step 2-&gt;3 trigger) over a real <see cref="Zone" />. This is what makes
///     <c>FishingProgressService.PollBite</c> (sub-action 1) reachable through normal play -- without it,
///     nothing ever sets <c>FishingStep</c> to 3 outside of the client's own unvalidated sub-action 3.
/// </summary>
public class FishingBiteWindowSystemTests
{
    private static int ReadResult(byte[] frame)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1 + 8));
    }

    private static int ReadFishingStep(byte[] frame)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1 + 16));
    }

    private static (Zone Zone, FakeDuplexPipe Pipe, PlayerRuntimeState State) SetUp(short mapId)
    {
        var zone = ZoneTestKit.CreateZone(mapId, simulationSystems: [new FishingBiteWindowSystem()]);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, pipe, state!);
    }

    /// <summary>
    ///     Casts (mirrored the same way <c>FishingLineService.Cast</c>'s success path does -- state=1, step=2,
    ///     cast timestamp stamped), then simulated real-world time is pushed past the 1-minute window entirely
    ///     by backdating the cast timestamp (no sleep needed), then ticks the zone with no client-sent
    ///     FishingProgress request anywhere in this test -- the step-3 push must still arrive.
    /// </summary>
    [Fact]
    public void Simulate_Step2CastOverAMinuteAgo_AdvancesToStep3AndPushesUnsolicited()
    {
        var (zone, pipe, state) = SetUp(FishingLineHandler.FishingZoneNumber);
        state.FishingState = 1;
        state.FishingStep = 2;
        state.FishingCastAtUtc = DateTime.UtcNow.AddMinutes(-2);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(3, state.FishingStep);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(FrameWriter.FrameSizeOf<FishingProgressResponse>(), bytes.Length);
        Assert.Equal(3, ReadResult(bytes));
        Assert.Equal(3, ReadFishingStep(bytes));
    }

    [Fact]
    public void Simulate_Step2CastLessThanAMinuteAgo_StaysAtStep2AndSendsNothing()
    {
        var (zone, pipe, state) = SetUp(FishingLineHandler.FishingZoneNumber);
        state.FishingState = 1;
        state.FishingStep = 2;
        state.FishingCastAtUtc = DateTime.UtcNow;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(2, state.FishingStep);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Simulate_NotAtStep2_NeverAdvancesEvenWithAnOldCastTimestamp()
    {
        var (zone, pipe, state) = SetUp(FishingLineHandler.FishingZoneNumber);
        state.FishingState = 1;
        state.FishingStep = 3;
        state.FishingCastAtUtc = DateTime.UtcNow.AddMinutes(-5);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(3, state.FishingStep);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Simulate_NoActiveFishingSession_NeverAdvances()
    {
        var (zone, pipe, state) = SetUp(FishingLineHandler.FishingZoneNumber);
        state.FishingState = 0;
        state.FishingStep = 2;
        state.FishingCastAtUtc = DateTime.UtcNow.AddMinutes(-5);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(2, state.FishingStep);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Simulate_WrongShard_NeverAdvancesEvenPastTheBiteWindow()
    {
        var (zone, pipe, state) = SetUp(1);
        state.FishingState = 1;
        state.FishingStep = 2;
        state.FishingCastAtUtc = DateTime.UtcNow.AddMinutes(-5);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(2, state.FishingStep);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}

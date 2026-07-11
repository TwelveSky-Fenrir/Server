using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Fishing;

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

using System.Buffers.Binary;
using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Handlers.FishingConsumables.Services;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Fishing;

/// <summary>Drives the real <see cref="FishingLineHandler" /> (opcode 103) over a real <see cref="Zone" />.</summary>
public class FishingLineHandlerTests
{
    private static FishingLineHandler CreateHandler()
    {
        return new FishingLineHandler(new FishingLineService());
    }

    private static int ReadResult(byte[] frame)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1 + 8));
    }

    [Fact]
    public void WrongZone_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        CreateHandler().Handle(new FishingLineRequest { Sort = 1, LocationX = 0, LocationZ = 0 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public void InvalidSort_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(FishingLineHandler.FishingZoneNumber);
        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, FishingLineHandler.FishingZoneNumber)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        CreateHandler().Handle(new FishingLineRequest { Sort = 3, LocationX = 0, LocationZ = 0 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public void Cast_NoGeometryLoaded_FailsAndDoesNotMutateState()
    {
        var zone = ZoneTestKit.CreateZone(FishingLineHandler.FishingZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, FishingLineHandler.FishingZoneNumber)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        CreateHandler().Handle(new FishingLineRequest { Sort = 1, LocationX = 0, LocationZ = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(0, state!.FishingState);
        Assert.False(state.CatchingFish);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(FrameWriter.FrameSizeOf<FishingLineResponse>(), bytes.Length);
        Assert.Equal(0, ReadResult(bytes));
    }

    [Fact]
    public void Reel_WhileFishing_ResetsStateAndRepliesResult2()
    {
        var zone = ZoneTestKit.CreateZone(FishingLineHandler.FishingZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, FishingLineHandler.FishingZoneNumber)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.FishingState = 1;
        state.FishingStep = 2;
        state.CatchingFish = true;

        CreateHandler().Handle(new FishingLineRequest { Sort = 2, LocationX = 0, LocationZ = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.FishingState);
        Assert.Equal(0, state.FishingStep);
        Assert.False(state.CatchingFish);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(FrameWriter.FrameSizeOf<FishingLineResponse>(), bytes.Length);
        Assert.Equal(2, ReadResult(bytes));
    }

    [Fact]
    public void Reel_NotFishing_RepliesResult0AndCatchingFishStaysCleared()
    {
        var zone = ZoneTestKit.CreateZone(FishingLineHandler.FishingZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, FishingLineHandler.FishingZoneNumber)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        CreateHandler().Handle(new FishingLineRequest { Sort = 2, LocationX = 0, LocationZ = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(0, state!.FishingState);
        Assert.False(state.CatchingFish);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(FrameWriter.FrameSizeOf<FishingLineResponse>(), bytes.Length);
        Assert.Equal(0, ReadResult(bytes));
    }
}

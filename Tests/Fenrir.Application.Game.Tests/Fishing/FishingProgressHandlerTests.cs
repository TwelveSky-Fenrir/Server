using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Services.FishingConsumables;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Framing;

namespace Fenrir.Application.Game.Tests.Fishing;

/// <summary>Drives the real <see cref="FishingProgressHandler" /> (opcode 104) over a real <see cref="Zone" />.</summary>
public class FishingProgressHandlerTests
{
    private static readonly int ProgressFrame = FrameWriter.FrameSizeOf<FishingProgressResponse>();
    private static readonly int ActionFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    private static int ReadResult(byte[] frame)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1 + 8));
    }

    private static int ReadFishingStep(byte[] frame)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1 + 16));
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(FishingLineHandler.FishingZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, FishingLineHandler.FishingZoneNumber)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, pipe, zone, state!);
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

        new FishingProgressHandler(new FishingProgressService()).Handle(new FishingProgressRequest { Sort = 2, FishingStep = 0 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public void InvalidSort_Aborts()
    {
        var (session, _, _, _) = SetUp();

        new FishingProgressHandler(new FishingProgressService()).Handle(new FishingProgressRequest { Sort = 4, FishingStep = 0 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public void ForceStep_OutOfRange_Aborts()
    {
        var (session, _, _, _) = SetUp();

        new FishingProgressHandler(new FishingProgressService()).Handle(new FishingProgressRequest { Sort = 3, FishingStep = 6 }, session);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public void ForceStep_InRange_SetsStepWithNoBroadcast()
    {
        var (session, pipe, zone, state) = SetUp();

        new FishingProgressHandler(new FishingProgressService()).Handle(new FishingProgressRequest { Sort = 3, FishingStep = 2 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(2, state.FishingStep);
        Assert.False(state.CatchingFish);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(ProgressFrame, bytes.Length);
        Assert.Equal(3, ReadResult(bytes));
    }

    [Fact]
    public void ForceStep_To4_SetsCatchingFishAndBroadcastsActionSort93()
    {
        var (session, pipe, zone, state) = SetUp();

        new FishingProgressHandler(new FishingProgressService()).Handle(new FishingProgressRequest { Sort = 3, FishingStep = 4 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(4, state.FishingStep);
        Assert.True(state.CatchingFish);
        Assert.Equal(93, state.ActionSort);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(ProgressFrame + ActionFrame, bytes.Length);
        Assert.Equal(3, ReadResult(bytes));
        Assert.Equal(4, ReadFishingStep(bytes));
    }

    [Fact]
    public void Recast_WhileFishing_RestampsCastClockAndSetsStep2()
    {
        var (session, pipe, zone, state) = SetUp();
        state.FishingState = 1;
        state.FishingStep = 5;
        state.FishingCastAtUtc = DateTime.UtcNow.AddMinutes(-5);

        new FishingProgressHandler(new FishingProgressService()).Handle(new FishingProgressRequest { Sort = 2, FishingStep = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(2, state.FishingStep);
        Assert.True(state.FishingCastAtUtc > DateTime.UtcNow.AddSeconds(-5));

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(ProgressFrame, bytes.Length);
        Assert.Equal(2, ReadResult(bytes));
    }

    [Fact]
    public void Recast_NotFishing_NoStateChangeButStillReplies()
    {
        var (session, pipe, zone, state) = SetUp();

        new FishingProgressHandler(new FishingProgressService()).Handle(new FishingProgressRequest { Sort = 2, FishingStep = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.FishingState);
        Assert.Equal(0, state.FishingStep);
        Assert.Null(state.FishingCastAtUtc);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(ProgressFrame, bytes.Length);
        Assert.Equal(2, ReadResult(bytes));
    }

    [Fact]
    public void PollBite_GateFails_NeverCast_IsSilent()
    {
        var (session, pipe, zone, state) = SetUp();
        state.FishingState = 1;
        state.FishingStep = 3;

        new FishingProgressHandler(new FishingProgressService()).Handle(new FishingProgressRequest { Sort = 1, FishingStep = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(3, state.FishingStep);
        Assert.False(state.CatchingFish);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void PollBite_GateFails_TooSoon_IsSilent()
    {
        var (session, pipe, zone, state) = SetUp();
        state.FishingState = 1;
        state.FishingStep = 3;
        state.FishingCastAtUtc = DateTime.UtcNow;

        new FishingProgressHandler(new FishingProgressService()).Handle(new FishingProgressRequest { Sort = 1, FishingStep = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(3, state.FishingStep);
        Assert.False(state.CatchingFish);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void PollBite_GateSucceeds_TransitionsToCatchStepAndBroadcasts()
    {
        var (session, pipe, zone, state) = SetUp();
        state.FishingState = 1;
        state.FishingStep = 3;
        state.FishingCastAtUtc = DateTime.UtcNow.AddMinutes(-2);

        new FishingProgressHandler(new FishingProgressService()).Handle(new FishingProgressRequest { Sort = 1, FishingStep = 0 }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(state.FishingStep is 4 or 5);
        Assert.True(state.CatchingFish);
        Assert.Equal(93, state.ActionSort);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(ProgressFrame + ActionFrame, bytes.Length);
        Assert.Equal(1, ReadResult(bytes));
    }
}

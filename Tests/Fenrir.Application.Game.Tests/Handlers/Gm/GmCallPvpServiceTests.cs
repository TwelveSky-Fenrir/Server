using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

public class GmCallPvpServiceTests
{
    private const int CallerId = 10;
    private const int TargetId = 20;
    private const int TargetAccountId = 200;
    private const int Sort = 599;

    private static (float X, float Y, float Z) Slot1Coordinate => (-232f, 36f, 2f);
    private static (float X, float Y, float Z) Slot2Coordinate => (232f, 36f, 2f);

    private static async Task AssertHeadFrameAsync<TPacket>(FakeDuplexPipe pipe, TPacket expected)
        where TPacket : struct, IOutgoingPacket
    {
        var actual = await PacketAssert.ReadSentBytesAsync(pipe);
        var frame = new byte[FrameWriter.FrameSizeOf<TPacket>()];
        FrameWriter.WriteFrame(in expected, frame);
        Assert.True(actual.Length >= frame.Length,
            $"Expected at least {frame.Length} bytes on the wire, got {actual.Length}.");
        Assert.Equal(frame, actual[..frame.Length]);
    }

    [Fact]
    public async Task HandleAsync_CallerNotBasicTier_AbortsWithNoReply_NoRelocation_NoAuditLog()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, _) = GmBasicTestSupport.Enter(zone, CallerId, "NotAGm");
        var (_, targetPipe, targetState) = GmBasicTestSupport.Enter(zone, TargetId, "Wanderer");
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);

        await service.HandleAsync(new GmCallPvpPayload { DuelSlot = 1, TargetName = "Wanderer" },
            GmBasicTestSupport.RequestData(), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        PacketAssert.AssertNothingSent(targetPipe);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Equal(100f, targetState.PosX);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task HandleAsync_InvalidDuelSlot_SendsFailureAck_NoRelocation_NoAuditLog(int duelSlot)
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, _) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var (_, targetPipe, targetState) = GmBasicTestSupport.Enter(zone, TargetId, "Wanderer");
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await service.HandleAsync(new GmCallPvpPayload { DuelSlot = duelSlot, TargetName = "Wanderer" }, data,
            session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });
        PacketAssert.AssertNothingSent(targetPipe);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Equal(100f, targetState.PosX);
    }

    [Fact]
    public async Task HandleAsync_NoMatchingConnectedCharacter_StillAcksSuccess_NoRelocation_NoAuditLog()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, _) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await service.HandleAsync(new GmCallPvpPayload { DuelSlot = 1, TargetName = "NobodyHome" }, data, session,
            CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_NameMatchIsCaseSensitive_DifferentCaseIsTreatedAsNoMatch()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, _) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var (_, targetPipe, targetState) = GmBasicTestSupport.Enter(zone, TargetId, "Wanderer");
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await service.HandleAsync(new GmCallPvpPayload { DuelSlot = 1, TargetName = "WANDERER" }, data, session,
            CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
        PacketAssert.AssertNothingSent(targetPipe);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Equal(100f, targetState.PosX);
        Assert.Equal(100f, targetState.PosZ);
    }

    [Fact]
    public async Task HandleAsync_ExactMatch_AcksCallerImmediately_ThenRelocatesTarget_AndLogsAuditRow()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, _) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var (_, targetPipe, targetState) =
            GmBasicTestSupport.Enter(zone, TargetId, "Wanderer", accountId: TargetAccountId);
        ZoneTestKit.DrainOutbound(pipe);
        ZoneTestKit.DrainOutbound(targetPipe);
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmCallPvpPayload { DuelSlot = 1, TargetName = "Wanderer" }, data, session,
                CancellationToken.None), zone);

        await AssertHeadFrameAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var (x, y, z) = Slot1Coordinate;
        Assert.Equal(x, targetState.PosX);
        Assert.Equal(y, targetState.PosY);
        Assert.Equal(z, targetState.PosZ);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)12, logged.EventCode);
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(GmBasicTestSupport.AccountId, logged.ActorAccountId);
        Assert.Equal(CallerId, logged.ActorCharacterId);
        Assert.Equal(TargetAccountId, logged.TargetAccountId);
        Assert.Equal(TargetId, logged.TargetCharacterId);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("DuelSlot=1;TargetName=Wanderer", logged.Payload);
    }

    [Fact]
    public async Task HandleAsync_MultipleConnectedCharactersShareExactName_AllAreIndependentlyRelocated()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, _, _) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var (_, _, firstState) = GmBasicTestSupport.Enter(zone, TargetId, "Twin", accountId: 201);
        var (_, _, secondState) = GmBasicTestSupport.Enter(zone, 21, "Twin", accountId: 202);
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmCallPvpPayload { DuelSlot = 2, TargetName = "Twin" },
                GmBasicTestSupport.RequestData(), session, CancellationToken.None), zone);

        var (x, y, z) = Slot2Coordinate;
        Assert.Equal(x, firstState.PosX);
        Assert.Equal(y, firstState.PosY);
        Assert.Equal(z, firstState.PosZ);
        Assert.Equal(x, secondState.PosX);
        Assert.Equal(y, secondState.PosY);
        Assert.Equal(z, secondState.PosZ);

        Assert.Equal(2, eventLog.LoggedEvents.Count);
    }

    [Fact]
    public async Task HandleAsync_CandidateAlreadyEngagedInDuelRelatedState_IsSkipped_NotRelocated()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, _, _) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var (_, _, targetState) = GmBasicTestSupport.Enter(zone, TargetId, "Busy", accountId: TargetAccountId);
        var duelRegistry = new DuelRegistry();
        duelRegistry.TryAsk(TargetId, 999, false);
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance, duelRegistry);

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmCallPvpPayload { DuelSlot = 1, TargetName = "Busy" },
                GmBasicTestSupport.RequestData(), session, CancellationToken.None), zone);

        Assert.Equal(100f, targetState.PosX);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_CallerNameExactlyMatchesOwnRequestedTarget_NoSelfExclusion_CallerIsRelocatedToo()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, _, callerState) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmCallPvpPayload { DuelSlot = 1, TargetName = "TheGm" },
                GmBasicTestSupport.RequestData(), session, CancellationToken.None), zone);

        var (x, y, z) = Slot1Coordinate;
        Assert.Equal(x, callerState.PosX);
        Assert.Equal(y, callerState.PosY);
        Assert.Equal(z, callerState.PosZ);
        Assert.Single(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_TargetOnADifferentZoneThanCaller_IsStillFoundAndRelocated()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1, 2]);
        var callerZone = registry[1];
        var targetZone = registry[2];
        var (session, _, _) = GmBasicTestSupport.Enter(callerZone, CallerId, "TheGm", 1);
        var (_, _, targetState) = GmBasicTestSupport.Enter(targetZone, TargetId, "Elsewhere");
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmCallPvpPayload { DuelSlot = 1, TargetName = "Elsewhere" },
                GmBasicTestSupport.RequestData(), session, CancellationToken.None), targetZone);

        var (x, y, z) = Slot1Coordinate;
        Assert.Equal(x, targetState.PosX);
        Assert.Equal(y, targetState.PosY);
        Assert.Equal(z, targetState.PosZ);
        Assert.Single(eventLog.LoggedEvents);
    }

    [Theory]
    [InlineData(1, -232f, 36f, 2f)]
    [InlineData(2, 232f, 36f, 2f)]
    public async Task HandleAsync_DuelSlotSelectsTheRecoveredFixedCoordinateTriple(int duelSlot, float expectedX,
        float expectedY, float expectedZ)
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, _, _) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var (_, _, targetState) = GmBasicTestSupport.Enter(zone, TargetId, "Wanderer");
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmCallPvpPayload { DuelSlot = duelSlot, TargetName = "Wanderer" },
                GmBasicTestSupport.RequestData(), session, CancellationToken.None), zone);

        Assert.Equal(expectedX, targetState.PosX);
        Assert.Equal(expectedY, targetState.PosY);
        Assert.Equal(expectedZ, targetState.PosZ);
    }
}

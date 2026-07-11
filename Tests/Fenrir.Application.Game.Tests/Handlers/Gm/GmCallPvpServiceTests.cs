using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
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

// GM-CALLPVP (legacy PROCESS_DATA_SEND, opcode 19, tSort 599 -- Server/ts25zone/S04_MyWork04.cpp:1770-1823).
// The success acknowledgment is sent to the calling GM BEFORE any relocation work runs -- the caller has no way
// to distinguish "found and moved" from "matched nobody" from the response alone. Matching is exact,
// case-sensitive, full-string equality (deliberately NOT the case-insensitive lookup this dispatch table's
// sibling by-name commands use). The two fixed relocation coordinates are documented, inert (0,0,0)
// placeholders pending real Server/ values -- see GmCallPvpService's own remarks; these tests assert the
// mechanism wires a matched target to whichever placeholder the requested duel slot resolves to, not that the
// placeholder itself is a real, tested location.
public class GmCallPvpServiceTests
{
    private const int CallerId = 10;
    private const int TargetId = 20;
    private const int TargetAccountId = 200;
    private const int Sort = 599;

    private static (float X, float Y, float Z) PlaceholderCoordinate => (0f, 0f, 0f);

    /// <summary>
    ///     Asserts the pipe's very next buffered frame (not necessarily the ONLY one) matches
    ///     <paramref name="expected" /> -- the head-of-buffer counterpart to GmBasicTestSupport's own
    ///     AssertTailFrameAsync, needed here because this command's own explicit ordering guarantee is
    ///     "ack sent before relocation work runs," not "ack sent last."
    /// </summary>
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
        Assert.Equal(100f, targetState.PosX); // ZoneTestKit.EnterData's own default, untouched
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

        // Success is unconditional on a valid duel slot -- there is no distinct "target not found" signal.
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

        // Deliberately NOT the case-insensitive match this dispatch table's sibling by-name commands use.
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
        ZoneTestKit.DrainOutbound(pipe); // target's own Enter-broadcast join packet, not under test
        ZoneTestKit.DrainOutbound(targetPipe);
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        // Ack must land on the caller's pipe as the FIRST frame -- it is sent before the relocation loop even
        // starts, not after it completes.
        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmCallPvpPayload { DuelSlot = 1, TargetName = "Wanderer" }, data, session,
                CancellationToken.None), zone);

        await AssertHeadFrameAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var (x, y, z) = PlaceholderCoordinate;
        Assert.Equal(x, targetState.PosX);
        Assert.Equal(y, targetState.PosY);
        Assert.Equal(z, targetState.PosZ);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)12, logged.EventCode); // GmDuelAndInventoryActionEventCodes.CallPvpRelocate (internal, not visible here)
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

        var (x, y, z) = PlaceholderCoordinate;
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
        // Puts TargetId into a pending duel negotiation -- DuelRegistry.IsNegotiating(TargetId) becomes true.
        duelRegistry.TryAsk(TargetId, 999, false);
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance, duelRegistry);

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmCallPvpPayload { DuelSlot = 1, TargetName = "Busy" },
                GmBasicTestSupport.RequestData(), session, CancellationToken.None), zone);

        Assert.Equal(100f, targetState.PosX); // untouched -- skipped, not a near-miss
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_CallerNameExactlyMatchesOwnRequestedTarget_NoSelfExclusion_CallerIsRelocatedToo()
    {
        // Deliberately NOT the self-exclusion this dispatch table's sibling by-name commands (FIND/CALL/MOVE/
        // NCHAT/YCHAT/KICK) apply via their own shared SearchAvatar-equivalent helper -- see GmCallPvpService's
        // own remarks. This command's citation iterates every connected character with no such guard.
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, _, callerState) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        var eventLog = new FakeEventLogRepository();
        var service = new GmCallPvpService(registry, eventLog, NullLogger<GmCallPvpService>.Instance);

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmCallPvpPayload { DuelSlot = 1, TargetName = "TheGm" },
                GmBasicTestSupport.RequestData(), session, CancellationToken.None), zone);

        var (x, y, z) = PlaceholderCoordinate;
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

        var (x, y, z) = PlaceholderCoordinate;
        Assert.Equal(x, targetState.PosX);
        Assert.Equal(y, targetState.PosY);
        Assert.Equal(z, targetState.PosZ);
        Assert.Single(eventLog.LoggedEvents);
    }
}

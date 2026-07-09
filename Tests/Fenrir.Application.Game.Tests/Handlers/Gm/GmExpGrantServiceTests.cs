using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

// Elevated-tier GM-EXP, "grant experience to self" (legacy PROCESS_DATA_SEND, opcode 19, tSort 503 --
// there is no dedicated legacy wire opcode for this command; GenericActionHandler decodes GmExpGrantPayload
// out of GenericActionRequest.Data before calling into this service -- Server/ts25zone/S04_MyWork04.cpp:
// 959-1005). GmExpGrantService itself always reports success once the tier gate passes (matching the cited
// case's own unconditional tResult=0); the actual character-experience mutation is applied by Zone.Gm.cs's
// mode-0 branch (Zone.ApplyGmCharacterExperienceGrant) on the zone's own tick thread. Covers: the
// Elevated-tier privilege gate, the three mode-0 branches (ordinary clamp -- both positive and the
// documented unclamped-negative edge case, already-at-cap no-op, and the huge-magnitude "instant max"
// shortcut), an unmodeled mode value's no-op dispatch, and the game.EventLog (Category=GmAction) audit row
// written on every accepted invocation regardless of the mode-0 branch taken.
public class GmExpGrantServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short MapId = 1;
    private const int GenericActionDataLength = 130;
    private const int Sort = 503;

    private static async Task RunToCompletionAsync(ValueTask pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("GmExpGrantService.HandleAsync never completed.");
        }

        await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeEventLogRepository EventLog) SetUp(short accountGrade)
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId, null, accountGrade);
        session.MarkRegistering();
        session.MarkInWorld();
        session.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (session, pipe, zone, state!, new FakeEventLogRepository());
    }

    private static byte[] RequestData(int type, int exp)
    {
        var data = new byte[GenericActionDataLength];
        new GmExpGrantPayload { Type = type, Exp = exp }.Write(data);
        return data;
    }

    /// <summary>
    ///     Tolerates the AvatarStatUpdateResponse (Exp1 stat push) Zone.ApplyCharacterExperienceGain sends to
    ///     this same caller's own session BEFORE this service's own ack, whenever mode-0 actually deposits an
    ///     amount -- same "only the response frame's own byte-exact shape is asserted, as the tail of whatever
    ///     is currently buffered" posture GmCreateItemServiceTests documents for its own ground-item-broadcast
    ///     ordering quirk.
    /// </summary>
    private static async Task AssertResponseSentAsync(FakeDuplexPipe pipe, GenericActionResponse expected)
    {
        var actual = await PacketAssert.ReadSentBytesAsync(pipe);
        var frame = new byte[FrameWriter.FrameSizeOf<GenericActionResponse>()];
        FrameWriter.WriteFrame(in expected, frame);
        Assert.True(actual.Length >= frame.Length,
            $"Expected at least {frame.Length} bytes on the wire, got {actual.Length}.");
        Assert.Equal(frame, actual[^frame.Length..]);
    }

    [Fact]
    public async Task HandleAsync_CallerNotElevatedTier_AbortsWithNoReply_LogsNothing_AndLeavesExperienceUnchanged()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Basic);
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, 5000);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = 5000 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Equal(0, state.Experience);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_Mode0_PositiveMagnitude_IncreasesExperienceByMagnitude_LogsAuditRow_AndAcksSuccess()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, 5000);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = 5000 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(5000, state.Experience);
        await AssertResponseSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)3, logged.EventCode); // GmActionEventCodes.ExpGrant (internal, not visible here).
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("Mode=0;Magnitude=5000", logged.Payload);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_Mode0_NegativeMagnitude_DecreasesExperienceUnclamped()
    {
        // Server/ts25zone/S07_MyGame03.cpp:177-180's own ordinary branch only clamps the UPPER bound -- a
        // negative magnitude flows through as a raw decrease, verified finding, not independently re-derived.
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        state.Experience = 10_000;
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, -4000);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = -4000 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Equal(6000, state.Experience);
        await AssertResponseSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("Mode=0;Magnitude=-4000", logged.Payload);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_Mode0_AlreadyAtMaximum_NoOp_ButStillLogsSuccessAndAcksAccepted()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        state.Experience = int.MaxValue; // GmMaxExperience.
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, 1);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = 1 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Equal(int.MaxValue, state.Experience);
        // No ApplyCharacterExperienceGain call on this branch -- no preceding AvatarStatUpdateResponse frame,
        // so (unlike the two tests above) the ack is the only thing on the wire.
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_Mode0_MagnitudeAtCeiling_InstantMaxShortcut_SetsExperienceToCeiling()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        Assert.Equal(0, state.Level2); // pre-cap, so the shortcut's second (toMax) deposit is eligible to run too.
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, int.MaxValue);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = int.MaxValue }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Equal(int.MaxValue, state.Experience);
        await AssertResponseSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal($"Mode=0;Magnitude={int.MaxValue}", logged.Payload);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_UnmodeledMode_LeavesExperienceUnchanged_ButStillAcksAndLogs()
    {
        // Mode 1 (pet experience -- covered by PetExperienceCreditResolverTests, not duplicated here) and mode
        // 3 (dead/commented-out legacy code) are the only other recognized values; anything else, like this
        // arbitrary 2, is silently ignored by Zone.ApplyGmSelfExperienceGrantCommand's switch.
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(2, 5000);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 2, Exp = 5000 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Equal(0, state.Experience);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("Mode=2;Magnitude=5000", logged.Payload);
    }
}

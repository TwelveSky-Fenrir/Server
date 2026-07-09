using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.World;
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

    // Zone.Gm.cs's own GmMaxExperience -- legacy's MAX_NUMBER_SIZE (Server/Header/Protocol/DEFINE.h:365),
    // not int.MaxValue. Duplicated here (the constant itself is private) so these regression tests assert
    // against the same literal the fix pins, rather than a re-derived value.
    private const long LegacyExperienceCeiling = 2_000_000_000;

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
        FakeEventLogRepository EventLog) SetUp(short accountGrade, WorldDataCache? worldData = null)
    {
        var zone = ZoneTestKit.CreateZone(MapId, worldData: worldData);
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
    public async Task
        HandleAsync_ElevatedTier_Mode0_PositiveMagnitude_IncreasesExperienceByMagnitude_LogsAuditRow_AndAcksSuccess()
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
        state.Experience = LegacyExperienceCeiling; // GmMaxExperience -- legacy MAX_NUMBER_SIZE, not int.MaxValue.
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, 1);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = 1 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Equal(LegacyExperienceCeiling, state.Experience);
        // No ApplyCharacterExperienceGain call on this branch -- no preceding AvatarStatUpdateResponse frame,
        // so (unlike the two tests above) the ack is the only thing on the wire.
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome);
    }

    /// <summary>
    ///     Regression for the already-at-cap check's own boundary: a total sitting strictly between legacy's
    ///     <c>MAX_NUMBER_SIZE</c> (2,000,000,000) and <see cref="int.MaxValue" /> (2,147,483,647) must ALSO be
    ///     treated as already-at-cap -- before the fix, <c>GmMaxExperience</c> was <see cref="int.MaxValue" />
    ///     itself, so this exact total would have fallen through to the ordinary deposit path instead of no-op'ing.
    /// </summary>
    [Fact]
    public async Task
        HandleAsync_ElevatedTier_Mode0_AboveLegacyCeilingButBelowIntMaxValue_StillTreatedAsAlreadyAtMaximum()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        state.Experience = LegacyExperienceCeiling + 1_000; // strictly above MAX_NUMBER_SIZE, still below int.MaxValue.
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, 1);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = 1 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Equal(LegacyExperienceCeiling + 1_000, state.Experience);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_Mode0_MagnitudeAtCeiling_InstantMaxShortcut_SetsExperienceToCeiling()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        Assert.Equal(0, state.Level2); // pre-cap, so the shortcut's second (toMax) deposit is eligible to run too.
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, (int)LegacyExperienceCeiling);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = (int)LegacyExperienceCeiling }, data,
                session, state, zone, CancellationToken.None), zone);

        Assert.Equal(LegacyExperienceCeiling, state.Experience);
        await AssertResponseSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal($"Mode=0;Magnitude={LegacyExperienceCeiling}", logged.Payload);
    }

    /// <summary>
    ///     Regression for the huge-magnitude shortcut's own trigger threshold: sending exactly legacy's
    ///     <c>MAX_NUMBER_SIZE</c> (2,000,000,000, the natural GM test value) must trigger the "instant max
    ///     level" shortcut -- which caps the deposit at the character's first-tier level cap when
    ///     <see cref="PlayerRuntimeState.Level2" /> is already non-zero -- rather than falling through to the
    ///     ordinary deposit path. Before the fix, <c>GmMaxExperience</c> was <see cref="int.MaxValue" />
    ///     (2,147,483,647), so this exact magnitude would NOT have cleared the shortcut's own
    ///     <c>magnitude &gt;= GmMaxExperience</c> trigger and would have fallen through to the ordinary path,
    ///     which ignores the per-level cap entirely and deposits the full raw magnitude (clamped only to the
    ///     buggy, higher ceiling) -- landing the character 500,000,000 experience above where legacy actually
    ///     stops it.
    /// </summary>
    [Fact]
    public async Task
        HandleAsync_ElevatedTier_Mode0_MagnitudeAtCeiling_WithSecondTierAlreadyStarted_ShortcutClampsToFirstTierCap_NotFullMagnitude()
    {
        var levels = new Dictionary<short, LevelRowDto>
        {
            [LevelProgressionCalculator.MaxLevel] =
                new(LevelProgressionCalculator.MaxLevel, 1_500_000_000, 1_999_999_999, 0, 0, 0, 0, 0, 0, 0, 0)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(levelsByLevel: levels);

        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated, worldData);
        state.Level2 = 1; // second-tier (God/Rebirth) progress already begun -- blocks the shortcut's toMax step.
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, (int)LegacyExperienceCeiling);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = (int)LegacyExperienceCeiling }, data,
                session, state, zone, CancellationToken.None), zone);

        // Shortcut fired and stopped at the first-tier level cap (1,500,000,000), NOT the ordinary path's raw
        // deposit of the full 2,000,000,000 magnitude.
        Assert.Equal(1_500_000_000, state.Experience);
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome);
    }

    /// <summary>
    ///     Regression for the ordinary path's own upper-bound clamp: a deposit that would push the running
    ///     total past legacy's <c>MAX_NUMBER_SIZE</c> (2,000,000,000) must be clamped there -- before the fix,
    ///     the clamp ceiling was <see cref="int.MaxValue" />, so this exact deposit would have overshot the
    ///     legacy cap by 4,000.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ElevatedTier_Mode0_OrdinaryDeposit_ClampsToLegacyCeiling_NotIntMaxValue()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        state.Experience = LegacyExperienceCeiling - 1_000;
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, 5_000);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = 5_000 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Equal(LegacyExperienceCeiling, state.Experience);
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome);
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

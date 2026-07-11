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

public class GmExpGrantServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short MapId = 1;
    private const int GenericActionDataLength = 130;
    private const int Sort = 503;

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
        Assert.Equal((short)3, logged.EventCode);
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
        state.Experience = LegacyExperienceCeiling;
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, 1);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = 1 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Equal(LegacyExperienceCeiling, state.Experience);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome);
    }

        [Fact]
    public async Task
        HandleAsync_ElevatedTier_Mode0_AboveLegacyCeilingButBelowIntMaxValue_StillTreatedAsAlreadyAtMaximum()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        state.Experience = LegacyExperienceCeiling + 1_000;
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
        Assert.Equal(0, state.Level2);
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
        state.Level2 = 1;
        var service = new GmExpGrantService(eventLog, NullLogger<GmExpGrantService>.Instance);
        var data = RequestData(0, (int)LegacyExperienceCeiling);

        await RunToCompletionAsync(
            service.HandleAsync(new GmExpGrantPayload { Type = 0, Exp = (int)LegacyExperienceCeiling }, data,
                session, state, zone, CancellationToken.None), zone);

        Assert.Equal(1_500_000_000, state.Experience);
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)1, logged.Outcome);
    }

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

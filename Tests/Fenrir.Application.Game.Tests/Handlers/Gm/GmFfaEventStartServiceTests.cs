using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

// Elevated-tier zone-wide FFA-start "GM-FFASTART" (legacy PROCESS_DATA_SEND, opcode 19, tSort 333 -- there
// is no dedicated legacy wire opcode for this command; GenericActionHandler decodes GmFfaEventStartPayload
// out of GenericActionRequest.Data before calling into this service -- Server/ts25zone/S04_MyWork04.cpp:
// 1097-1131). GmFfaEventStartService only reproduces the two process-local writes the cited case body
// performs (Zone335StartTrigger's countdown + start-trigger flag) once its own idle-state precondition
// (ZoneCenterSiegeState.Zone335 == 0) passes -- the FFA-335 autonomous consuming tick is a separate,
// unmodeled subsystem (see that service's own remarks). Covers: the Elevated-tier privilege gate, the
// idle-vs-busy acceptance/rejection branch, the zero/negative-duration-defaults-to-ten-minutes rule, and the
// game.EventLog (Category=GmAction) audit row written on every invocation past the tier gate regardless of
// whether the idle check accepted or rejected the request.
public class GmFfaEventStartServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const int Sort = 333;
    private const int GenericActionDataLength = 130;

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, ZoneCenterSiegeState SiegeState,
        Zone335StartTrigger StartTrigger, FakeEventLogRepository EventLog) SetUp(short accountGrade)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId, null, accountGrade);
        return (session, pipe, new ZoneCenterSiegeState(), new Zone335StartTrigger(), new FakeEventLogRepository());
    }

    private static byte[] RequestData(int time)
    {
        var data = new byte[GenericActionDataLength];
        new GmFfaEventStartPayload { Time = time }.Write(data);
        return data;
    }

    [Fact]
    public async Task HandleAsync_CallerNotElevatedTier_AbortsWithNoReply_LogsNothing_AndDoesNotArmTrigger()
    {
        var (session, pipe, siegeState, startTrigger, eventLog) = SetUp((short)GmCommandTier.Basic);
        var service = new GmFfaEventStartService(siegeState, startTrigger, eventLog,
            NullLogger<GmFfaEventStartService>.Instance);
        var data = RequestData(5);

        await service.HandleAsync(new GmFfaEventStartPayload { Time = 5 }, data, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.False(startTrigger.StartRequested);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_IdleFfaState_ArmsTriggerWithRequestedDuration_AcksAccepted_AndLogsAuditRow()
    {
        var (session, pipe, siegeState, startTrigger, eventLog) = SetUp((short)GmCommandTier.Elevated);
        Assert.Equal(0, siegeState.Zone335); // idle by construction.
        var service = new GmFfaEventStartService(siegeState, startTrigger, eventLog,
            NullLogger<GmFfaEventStartService>.Instance);
        var data = RequestData(5);

        await service.HandleAsync(new GmFfaEventStartPayload { Time = 5 }, data, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(startTrigger.StartRequested);
        Assert.Equal(SimulationClock.ToWholeLegacyTicks(TimeSpan.FromMinutes(5)), startTrigger.RemainingTicks);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)5, logged.EventCode); // GmActionEventCodes.FfaEventStart (internal, not visible here).
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("RequestedDurationMinutes=5;Accepted=True", logged.Payload);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_ZeroDuration_DefaultsToTenMinuteCountdown()
    {
        var (session, _, siegeState, startTrigger, eventLog) = SetUp((short)GmCommandTier.Elevated);
        var service = new GmFfaEventStartService(siegeState, startTrigger, eventLog,
            NullLogger<GmFfaEventStartService>.Instance);

        await service.HandleAsync(new GmFfaEventStartPayload { Time = 0 }, RequestData(0), session,
            CancellationToken.None);

        Assert.True(startTrigger.StartRequested);
        Assert.Equal(SimulationClock.ToWholeLegacyTicks(TimeSpan.FromMinutes(10)), startTrigger.RemainingTicks);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_FfaStateNotIdle_RejectsRequest_DoesNotArmTrigger_ButStillLogsAuditRow()
    {
        var (session, pipe, siegeState, startTrigger, eventLog) = SetUp((short)GmCommandTier.Elevated);
        siegeState.SetZone335(1501); // any non-idle phase value.
        var service = new GmFfaEventStartService(siegeState, startTrigger, eventLog,
            NullLogger<GmFfaEventStartService>.Instance);
        var data = RequestData(5);

        await service.HandleAsync(new GmFfaEventStartPayload { Time = 5 }, data, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.False(startTrigger.StartRequested);
        Assert.Equal(0, startTrigger.RemainingTicks);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)0, logged.Outcome);
        Assert.Equal("RequestedDurationMinutes=5;Accepted=False", logged.Payload);
    }
}

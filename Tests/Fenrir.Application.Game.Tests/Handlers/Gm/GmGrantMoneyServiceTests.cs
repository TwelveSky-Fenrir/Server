using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

// Elevated-tier GM "grant money" (legacy PROCESS_DATA_SEND, opcode 19, tSort 504 -- there is no dedicated
// legacy wire opcode for this command; GenericActionHandler routes tSort 504 straight into this service
// without decoding any payload out of GenericActionRequest.Data, since none is read --
// Server/ts25zone/S04_MyWork04.cpp:1006-1035). Everything past the tier gate in the cited legacy case body
// is dead/commented-out source, so GmGrantMoneyService credits no money at all (there is deliberately no
// money/cash repository dependency on this type to even make that possible) and echoes tResult's own
// never-reassigned function-entry default of 1 -- NOT a "rejected" sentinel, just what the compiled legacy
// binary actually sends back. Covers: the Elevated-tier privilege gate, the no-op-but-still-audited grant
// path, and the game.EventLog (Category=GmAction) audit row this Fenrir-authored addition writes so an
// otherwise-invisible privileged no-op is still traceable.
public class GmGrantMoneyServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const int Sort = 504;
    private const int GenericActionDataLength = 130;

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, FakeEventLogRepository EventLog) SetUp(
        short accountGrade)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId, null, accountGrade);
        return (session, pipe, new FakeEventLogRepository());
    }

    private static byte[] RequestData()
    {
        return new byte[GenericActionDataLength];
    }

    [Fact]
    public async Task HandleAsync_CallerNotElevatedTier_AbortsWithNoReply_AndLogsNothing()
    {
        var (session, pipe, eventLog) = SetUp((short)GmCommandTier.Basic);
        var service = new GmGrantMoneyService(eventLog, NullLogger<GmGrantMoneyService>.Instance);

        await service.HandleAsync(RequestData(), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_ElevatedTier_GrantsNoMoney_EchoesLegacysNonOverwrittenResult_AndLogsNoOpAuditRow()
    {
        var (session, pipe, eventLog) = SetUp((short)GmCommandTier.Elevated);
        var service = new GmGrantMoneyService(eventLog, NullLogger<GmGrantMoneyService>.Instance);
        var data = RequestData();

        await service.HandleAsync(data, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        // Result=1 here is legacy's own tResult function-entry default, never reassigned by the (entirely
        // dead) case 504 body -- not a deliberately-chosen rejection code.
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)4, logged.EventCode); // GmActionEventCodes.GrantMoney (internal, not visible here).
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Null(logged.DeltaMoney);
        Assert.Null(logged.DeltaBigMoney);
        // Outcome=0 (no-op), not 1 (success) -- there is no live effect this audit row could claim succeeded.
        Assert.Equal((byte)0, logged.Outcome);
        Assert.Contains("no money granted", logged.Payload);
    }
}

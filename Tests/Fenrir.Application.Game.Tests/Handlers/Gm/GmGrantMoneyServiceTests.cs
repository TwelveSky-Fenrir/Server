using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

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
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)4, logged.EventCode);
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Null(logged.DeltaMoney);
        Assert.Null(logged.DeltaBigMoney);
        Assert.Equal((byte)0, logged.Outcome);
        Assert.Contains("no money granted", logged.Payload);
    }
}

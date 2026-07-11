using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

public class GmSetPvpPointServiceTests
{
    private const int Sort = 598;
    private static readonly byte[] Data = new byte[130];

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe) CreateSession(short accountGrade)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(100, 10, null, accountGrade);
        return (session, pipe);
    }

    [Fact]
    public async Task HandleAsync_CallerNotBasicTier_AbortsWithNoReply()
    {
        var (session, pipe) = CreateSession(0);
        var service = new GmSetPvpPointService(NullLogger<GmSetPvpPointService>.Instance);

        await service.HandleAsync(new GmSetPvpPointPayload { DuelSlot = 1, PointValue = 500 }, Data, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task HandleAsync_InvalidDuelSlot_SendsFailureAck_NoDisconnect(int duelSlot)
    {
        var (session, pipe) = CreateSession(1);
        var service = new GmSetPvpPointService(NullLogger<GmSetPvpPointService>.Instance);

        await service.HandleAsync(new GmSetPvpPointPayload { DuelSlot = duelSlot, PointValue = 12345 }, Data,
            session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = Sort, Data = Data, RuneValue = 0 });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task HandleAsync_ValidDuelSlot_SendsSuccessAck_RegardlessOfPointValue(int duelSlot)
    {
        var (session, pipe) = CreateSession(1);
        var service = new GmSetPvpPointService(NullLogger<GmSetPvpPointService>.Instance);

        await service.HandleAsync(new GmSetPvpPointPayload { DuelSlot = duelSlot, PointValue = int.MinValue },
            Data, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = Data, RuneValue = 0 });
    }
}

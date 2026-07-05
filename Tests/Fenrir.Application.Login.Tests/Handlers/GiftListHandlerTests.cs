using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Handlers.Services;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

// op25 CL_GIFT_INFO_SEND -- the 10-page gift list, backed by the real game.Gifts pending queue.
public class ClGiftInfoSendHandlerTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task HandleAsync_NoPendingGifts_RepliesResultZeroWithEmptyMatrix()
    {
        var handler = new GiftListHandler(new GiftListService(FakeGiftRepository.Empty()));
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(new GiftListRequest(), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new GiftListResponse { Result = 0, GiftItem = new int[20] });
    }

    [Fact]
    public async Task HandleAsync_PendingGifts_FillsOldestFirstWithProductIdAndZeroSecondColumn()
    {
        var handler = new GiftListHandler(new GiftListService(FakeGiftRepository.WithPending((1, 1211), (2, 99700))));
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(new GiftListRequest(), session, CancellationToken.None);

        var expected = new int[20];
        expected[0] = 1211;
        expected[2] = 99700;
        await PacketAssert.AssertSentAsync(pipe, new GiftListResponse { Result = 0, GiftItem = expected });
    }

    [Fact]
    public async Task HandleAsync_MoreThanTenPendingGifts_OnlyShowsFirstTen()
    {
        var pending = Enumerable.Range(1, 15).Select(i => (i, (int?)i)).ToArray();
        var handler = new GiftListHandler(new GiftListService(FakeGiftRepository.WithPending(pending)));
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(new GiftListRequest(), session, CancellationToken.None);

        var expected = new int[20];
        for (var i = 0; i < 10; i++)
            expected[i * 2] = i + 1;
        await PacketAssert.AssertSentAsync(pipe, new GiftListResponse { Result = 0, GiftItem = expected });
    }

    private static (LoginClientSession Session, FakeDuplexPipe Pipe) CreateSessionInCharSelect()
    {
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(AccountId);
        session.MarkCharSelect();
        return (session, pipe);
    }
}

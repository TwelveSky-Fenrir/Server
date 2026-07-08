using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.ClaimGift;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Handlers;

// op21 CL_WANT_GIFT_SEND -- gift claim backed by the real game.Gifts pending queue + usp_Gift_ClaimIntoVault.
public class ClWantGiftSendHandlerTests
{
    [Fact]
    public async Task HandleAsync_IndexWithinPendingList_ClaimsThatGiftAndRepliesResultZero()
    {
        var gifts = FakeGiftRepository.WithPending((501, 1211), (502, 99700));
        var handler = new ClaimGiftHandler(new ClaimGiftService(gifts, NullLogger<ClaimGiftService>.Instance),
            NullLogger<ClaimGiftHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(new ClaimGiftRequest { Sort = 0, GiftInfoIndex = 1 }, session,
            CancellationToken.None);

        Assert.Equal(502, gifts.LastClaimedGiftId);
        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new ClaimGiftResponse { Result = 0 });
    }

    [Fact]
    public async Task HandleAsync_IndexBeyondPendingList_RepliesResultOneWithoutClaiming()
    {
        var gifts = FakeGiftRepository.WithPending((501, 1211));
        var handler = new ClaimGiftHandler(new ClaimGiftService(gifts, NullLogger<ClaimGiftService>.Instance),
            NullLogger<ClaimGiftHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(new ClaimGiftRequest { Sort = 0, GiftInfoIndex = 5 }, session,
            CancellationToken.None);

        Assert.Null(gifts.LastClaimedGiftId);
        await PacketAssert.AssertSentAsync(pipe, new ClaimGiftResponse { Result = 1 });
    }

    [Fact]
    public async Task HandleAsync_NoPendingGifts_RepliesResultOne()
    {
        var gifts = FakeGiftRepository.Empty();
        var handler = new ClaimGiftHandler(new ClaimGiftService(gifts, NullLogger<ClaimGiftService>.Instance),
            NullLogger<ClaimGiftHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(new ClaimGiftRequest { Sort = 0, GiftInfoIndex = 0 }, session,
            CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new ClaimGiftResponse { Result = 1 });
    }

    [Fact]
    public async Task HandleAsync_ClaimThrows_RepliesResultTwoWithoutDisconnecting()
    {
        var gifts = FakeGiftRepository.ThrowingOnClaim(new InvalidOperationException("vault full"), (501, 1211));
        var handler = new ClaimGiftHandler(new ClaimGiftService(gifts, NullLogger<ClaimGiftService>.Instance),
            NullLogger<ClaimGiftHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(new ClaimGiftRequest { Sort = 0, GiftInfoIndex = 0 }, session,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new ClaimGiftResponse { Result = 2 });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public async Task HandleAsync_IndexOutOfRange_AbortsAsMalformed(int index)
    {
        var gifts = FakeGiftRepository.Empty();
        var handler = new ClaimGiftHandler(new ClaimGiftService(gifts, NullLogger<ClaimGiftService>.Instance),
            NullLogger<ClaimGiftHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(new ClaimGiftRequest { Sort = 0, GiftInfoIndex = index }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    private static (LoginClientSession Session, FakeDuplexPipe Pipe) CreateSessionInCharSelect()
    {
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(1);
        session.MarkCharSelect();
        return (session, pipe);
    }
}

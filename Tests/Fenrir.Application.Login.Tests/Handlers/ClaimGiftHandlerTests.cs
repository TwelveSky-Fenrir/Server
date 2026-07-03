using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

/// <summary>
///     op21 CL_WANT_GIFT_SEND — V1 gift claim (login protocol report §4.21). GIFT_V2 is off in EU33, so every
///     page is empty for the whole population until the web-API path lands (chantier V8): Result=1 is the exact
///     legacy answer today, not a placeholder.
/// </summary>
public class ClWantGiftSendHandlerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public async Task Handle_IndexInRange_AlwaysRepliesNoGift(int index)
    {
        var handler = new ClaimGiftHandler();
        var (session, pipe) = CreateSessionInCharSelect();

        handler.Handle(new ClaimGiftRequest { Sort = 0, GiftInfoIndex = index }, session);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new ClaimGiftResponse { Result = 1 });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Handle_IndexOutOfRange_AbortsAsMalformed(int index)
    {
        var handler = new ClaimGiftHandler();
        var (session, pipe) = CreateSessionInCharSelect();

        handler.Handle(new ClaimGiftRequest { Sort = 0, GiftInfoIndex = index }, session);

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

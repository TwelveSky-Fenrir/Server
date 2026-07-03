using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

/// <summary>
///     op25 CL_GIFT_INFO_SEND — the 10-page gift list (login protocol report §4.25). GIFT_V2 is off in EU33 and
///     the web-API gift path is deferred to chantier V8, so the byte-exact legacy answer for every current
///     account is a static Result=0 with an all-zero matrix.
/// </summary>
public class ClGiftInfoSendHandlerTests
{
    [Fact]
    public async Task Handle_AlwaysRepliesResultZeroWithEmptyMatrix()
    {
        var handler = new GiftListHandler();
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(1);
        session.MarkCharSelect();

        handler.Handle(new GiftListRequest(), session);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new GiftListResponse { Result = 0, GiftItem = new int[20] });
    }
}

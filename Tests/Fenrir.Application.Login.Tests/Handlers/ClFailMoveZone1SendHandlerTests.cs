using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

/// <summary>
///     op23 CL_FAIL_MOVE_ZONE_1_SEND — rollback to CharSelect when the client couldn't reach the zone it was
///     redirected to (login protocol report §4.23). Legacy: no reply at all.
/// </summary>
public class ClFailMoveZone1SendHandlerTests
{
    [Fact]
    public void Handle_RollsSessionBackToCharSelect_AndRepliesNothing()
    {
        var handler = new ClFailMoveZone1SendHandler();
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(1);
        session.MarkCharSelect();
        session.MarkHandoverIssued(); // op22 already redirected this session to a zone

        handler.Handle(new ClFailMoveZone1Send(), session);

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }
}

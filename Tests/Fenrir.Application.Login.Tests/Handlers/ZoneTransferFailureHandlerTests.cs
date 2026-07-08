using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Handlers;

// op23 CL_FAIL_MOVE_ZONE_1_SEND -- rollback to CharSelect when the client couldn't reach the zone it was
// redirected to. Legacy: no reply at all.
public class ClFailMoveZone1SendHandlerTests
{
    [Fact]
    public void Handle_RollsSessionBackToCharSelect_AndRepliesNothing()
    {
        var handler = new ZoneTransferFailureHandler(NullLogger<ZoneTransferFailureHandler>.Instance);
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(1);
        session.MarkCharSelect();
        session.MarkHandoverIssued(); // op22 already redirected this session to a zone

        handler.Handle(new ZoneTransferFailureRequest(), session);

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }
}

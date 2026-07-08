using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Handlers;

// op24 CL_CHANGE_MASTER_SEND -- legacy body is empty (S04_MyWork02.cpp l.1655-1658): no read, no reply, no
// Quit, no state change.
public class ClChangeMasterSendHandlerTests
{
    [Fact]
    public void Handle_IsATotalNoOp()
    {
        var handler = new ChangeMasterHandler(NullLogger<ChangeMasterHandler>.Instance);
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(1);
        session.MarkCharSelect();

        handler.Handle(new ChangeMasterRequest { AvatarPost = 0, MasterId = "SomeMaster" }, session);

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }
}

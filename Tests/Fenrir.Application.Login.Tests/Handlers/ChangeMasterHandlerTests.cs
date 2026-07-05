using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

// op24 CL_CHANGE_MASTER_SEND -- legacy body is empty (S04_MyWork02.cpp l.1655-1658): no read, no reply, no
// Quit, no state change.
public class ClChangeMasterSendHandlerTests
{
    [Fact]
    public void Handle_IsATotalNoOp()
    {
        var handler = new ChangeMasterHandler();
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

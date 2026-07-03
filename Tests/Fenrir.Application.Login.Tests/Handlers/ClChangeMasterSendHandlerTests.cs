using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

/// <summary>
///     op24 CL_CHANGE_MASTER_SEND — faithfully dead: the legacy body is 100% empty (S04_MyWork02.cpp l.1655-1658),
///     no read, no reply, no Quit, no state change of any kind.
/// </summary>
public class ClChangeMasterSendHandlerTests
{
    [Fact]
    public void Handle_IsATotalNoOp()
    {
        var handler = new ClChangeMasterSendHandler();
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(1);
        session.MarkCharSelect();

        handler.Handle(new ClChangeMasterSend { AvatarPost = 0, MasterId = "SomeMaster" }, session);

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }
}

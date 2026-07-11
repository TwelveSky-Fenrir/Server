using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Login.Wire;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Handlers;

public class LoginKeepAliveHandlerTests
{
    [Fact]
    public void Handle_PromotesAuthenticatedToCharSelect_AndRepliesNothing()
    {
        var handler = new LoginKeepAliveHandler(NullLogger<LoginKeepAliveHandler>.Instance);
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(1);

        handler.Handle(new LoginKeepAliveRequest(), session);

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public void Handle_LeavesPinRequiredSessionUntouched_NeverBypassingThePinGate()
    {
        var handler = new LoginKeepAliveHandler(NullLogger<LoginKeepAliveHandler>.Instance);
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(1);
        session.MarkPinRequired();

        handler.Handle(new LoginKeepAliveRequest(), session);

        Assert.Equal(LoginSessionState.PinRequired, session.State);
        Assert.Null(session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public void Handle_LeavesCharSelectSessionUntouched()
    {
        var handler = new LoginKeepAliveHandler(NullLogger<LoginKeepAliveHandler>.Instance);
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(1);
        session.MarkCharSelect();

        handler.Handle(new LoginKeepAliveRequest(), session);

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }
}

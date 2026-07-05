using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Network.Tests.Sessions;

// IsOpcodeAllowed must reflect the generated SessionStateGate table exactly -- the sole gate the session
// loop relies on to reject out-of-sequence packets before dispatch.
public class ClientSessionStateTests
{
    // LoginRequest: AllowedStates = [Connected, VersionOk].
    [Fact]
    public void Login_LoginSend_AllowedWhileConnected_ForbiddenAfterCharSelect()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.Login));

        session.MarkCharSelect();

        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.Login));
    }

    // CreateAvatarRequest: AllowedStates = [Authenticated, CharSelect].
    [Fact]
    public void Login_CreateAvatarSend2_ForbiddenWhileConnected_AllowedAfterAuthenticated()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());

        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatar));

        session.MarkAuthenticated(1);

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatar));
    }

    // ZoneHandshakeRequest: AllowedStates = [Connected] only.
    [Fact]
    public void Zone_TempRegisterSend_AllowedOnlyWhileConnected()
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());

        Assert.True(session.IsOpcodeAllowed(Opcodes.Zone.Incoming.ZoneHandshake));

        session.MarkTicketConsumed(1, 1);

        Assert.False(session.IsOpcodeAllowed(Opcodes.Zone.Incoming.ZoneHandshake));
    }
}

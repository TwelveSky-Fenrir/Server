using Fenrir.Contracts;
using Fenrir.Network.Sessions;

namespace Fenrir.Network.Tests.Sessions;

/// <summary>
///     <c>IsOpcodeAllowed</c> must reflect the generated <c>SessionStateGate</c> table exactly (built from each
///     packet's <c>[FenrirPacket(AllowedStates = [...])]</c>) — this is the sole gate the session loop relies on to
///     reject out-of-sequence packets before dispatch.
/// </summary>
public class ClientSessionStateTests
{
    // LoginRequest: AllowedStates = [Connected, VersionOk] — legal at the start of the login flow, no longer legal
    // once the account is authenticated further down the flow.
    [Fact]
    public void Login_LoginSend_AllowedWhileConnected_ForbiddenAfterCharSelect()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.Login));

        session.MarkCharSelect();

        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.Login));
    }

    // CreateAvatarRequest: AllowedStates = [Authenticated, CharSelect] — illegal before credentials are verified.
    [Fact]
    public void Login_CreateAvatarSend2_ForbiddenWhileConnected_AllowedAfterAuthenticated()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());

        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatar));

        session.MarkAuthenticated(1);

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatar));
    }

    // ZoneHandshakeRequest: AllowedStates = [Connected] only — the ticket hand-off packet is one-shot.
    [Fact]
    public void Zone_TempRegisterSend_AllowedOnlyWhileConnected()
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());

        Assert.True(session.IsOpcodeAllowed(Opcodes.Zone.Incoming.ZoneHandshake));

        session.MarkTicketConsumed(1, 1);

        Assert.False(session.IsOpcodeAllowed(Opcodes.Zone.Incoming.ZoneHandshake));
    }
}

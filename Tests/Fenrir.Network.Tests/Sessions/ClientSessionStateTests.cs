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

    // Cross-process duplicate-login kick/refusal: MarkAccountSessionToken records the token
    // usp_AccountSession_ClaimOrSignalKick minted for this login epoch.
    [Fact]
    public void Login_MarkAccountSessionToken_SetsTheToken()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        var token = Guid.NewGuid();

        Assert.Null(session.AccountSessionToken);

        session.MarkAccountSessionToken(token);

        Assert.Equal(token, session.AccountSessionToken);
    }

    // The two-arg overload (every pre-existing call site) must keep leaving AccountSessionToken unset --
    // only ZoneHandshakeHandler's real ticket-consume path supplies a token.
    [Fact]
    public void Zone_MarkTicketConsumed_TwoArgOverload_LeavesAccountSessionTokenNull()
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());

        session.MarkTicketConsumed(1, 10);

        Assert.Null(session.AccountSessionToken);
    }

    [Fact]
    public void Zone_MarkTicketConsumed_WithToken_SetsAccountSessionToken()
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());
        var token = Guid.NewGuid();

        session.MarkTicketConsumed(1, 10, token);

        Assert.Equal(token, session.AccountSessionToken);
    }

    // GM-BLOCK precondition: every pre-existing 2/3-arg MarkTicketConsumed call site must default AccountGrade
    // to 0 (not elevated) rather than fail to compile or silently misbehave.
    [Fact]
    public void Zone_MarkTicketConsumed_TwoArgOverload_LeavesAccountGradeZero_AndIsGmFalse()
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());

        session.MarkTicketConsumed(1, 10);

        Assert.Equal((short)0, session.AccountGrade);
        Assert.False(session.IsGm);
    }

    [Fact]
    public void Zone_MarkTicketConsumed_WithGmGrade_SetsAccountGrade_AndIsGmTrue()
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());

        session.MarkTicketConsumed(1, 10, null, 1);

        Assert.Equal((short)1, session.AccountGrade);
        Assert.True(session.IsGm);
    }

    [Fact]
    public void Login_MarkAuthenticated_TwoArgCallSite_LeavesAccountGradeZero()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());

        session.MarkAuthenticated(1);

        Assert.Equal((short)0, session.AccountGrade);
    }

    [Fact]
    public void Login_MarkAuthenticated_WithGmGrade_SetsAccountGrade()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());

        session.MarkAuthenticated(1, 1);

        Assert.Equal((short)1, session.AccountGrade);
    }
}

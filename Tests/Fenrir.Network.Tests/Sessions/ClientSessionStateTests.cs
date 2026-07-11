using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Network.Tests.Sessions;

public class ClientSessionStateTests
{
    [Fact]
    public void Login_LoginSend_AllowedWhileConnected_ForbiddenAfterCharSelect()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.Loggedin));

        session.MarkCharSelect();

        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.Loggedin));
    }

    [Fact]
    public void Login_CreateAvatarSend2_ForbiddenWhileConnected_AllowedAfterAuthenticated()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());

        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatar));

        session.MarkAuthenticated(1);

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatar));
    }

    [Fact]
    public void Zone_TempRegisterSend_AllowedOnlyWhileConnected()
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());

        Assert.True(session.IsOpcodeAllowed(Opcodes.Zone.Incoming.ZoneHandshake));

        session.MarkTicketConsumed(1, 1);

        Assert.False(session.IsOpcodeAllowed(Opcodes.Zone.Incoming.ZoneHandshake));
    }

    [Fact]
    public void Login_MarkAccountSessionToken_SetsTheToken()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        var token = Guid.NewGuid();

        Assert.Null(session.AccountSessionToken);

        session.MarkAccountSessionToken(token);

        Assert.Equal(token, session.AccountSessionToken);
    }

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

    [Theory]
    [InlineData(0, false, false, false)]
    [InlineData(1, true, false, false)]
    [InlineData(9, true, false, false)]
    [InlineData(10, true, true, false)]
    [InlineData(99, true, true, false)]
    [InlineData(100, true, true, true)]
    public void Zone_MeetsGmTier_GatesEachThresholdIndependently(int grade, bool basic, bool elevated, bool admin)
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());

        session.MarkTicketConsumed(1, 10, null, (short)grade);

        Assert.Equal(basic, session.MeetsGmTier(GmCommandTier.Basic));
        Assert.Equal(elevated, session.MeetsGmTier(GmCommandTier.Elevated));
        Assert.Equal(admin, session.MeetsGmTier(GmCommandTier.Admin));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Zone_IsGm_MatchesMeetsGmTierBasic(int grade)
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());

        session.MarkTicketConsumed(1, 10, null, (short)grade);

        Assert.Equal(session.MeetsGmTier(GmCommandTier.Basic), session.IsGm);
    }

    [Fact]
    public void Zone_IsCrossShardTransferPending_DefaultsFalse_AndIsSetByMarkCrossShardTransferPending()
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());

        Assert.False(session.IsCrossShardTransferPending);

        session.MarkCrossShardTransferPending();

        Assert.True(session.IsCrossShardTransferPending);
    }

    [Fact]
    public void Zone_MarkCrossShardTransferPending_NeverChangesState()
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());
        session.MarkTicketConsumed(1, 10);
        session.MarkRegistering();
        session.MarkInWorld();
        var stateBefore = session.State;

        session.MarkCrossShardTransferPending();

        Assert.Equal(stateBefore, session.State);
    }
}

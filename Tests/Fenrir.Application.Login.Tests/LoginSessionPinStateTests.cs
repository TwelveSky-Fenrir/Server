using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Application.Login.Tests;

// The PIN gate state machine between Authenticated and CharSelect (P2ndPassword=1): LoginClientSession's
// state transitions and the SessionStateGate opcode gates that key off them.
public class LoginSessionPinStateTests
{
    [Fact]
    public void MarkPinRequired_TransitionsFromAuthenticated_AndResetsFailureCount()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        session.MarkAuthenticated(1);

        Assert.Equal(LoginSessionState.Authenticated, session.State);

        session.MarkPinRequired();

        Assert.Equal(LoginSessionState.PinRequired, session.State);
        Assert.Equal(0, session.PinFailureCount);
    }

    [Fact]
    public void RegisterPinFailure_IncrementsMonotonically()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        session.MarkAuthenticated(1);
        session.MarkPinRequired();

        Assert.Equal(1, session.RegisterPinFailure());
        Assert.Equal(2, session.RegisterPinFailure());
        Assert.Equal(3, session.RegisterPinFailure());
    }

    [Fact]
    public void PinRequired_OnlyAllowsKeepaliveAndThePinOpcodes()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        session.MarkAuthenticated(1);
        session.MarkPinRequired();

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.LoginKeepAlive));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateMousePin));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ChangeMousePin));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.VerifyMousePin));

        // Legacy Quit() on !IsSecondLogin(): char-select opcodes stay closed until the PIN gate opens.
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatar));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.DeleteAvatar));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.RenameAvatar));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ClaimGift));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ZoneTransfer));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.GiftList));
    }

    [Fact]
    public void MarkCharSelect_FromPinRequired_OpensTheCharSelectOpcodesAndClosesThePinOnes()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        session.MarkAuthenticated(1);
        session.MarkPinRequired();

        session.MarkCharSelect();

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatar));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.RenameAvatar));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ClaimGift));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.GiftList));

        // Ops 13/14/15 are only legal while the gate is still closed.
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateMousePin));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ChangeMousePin));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.VerifyMousePin));
    }

    [Fact]
    public void Authenticated_WithoutPinRequired_AlreadyAllowsCharSelectOpcodes()
    {
        // RequireSecondPassword=false path: the session never visits PinRequired at all.
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        session.MarkAuthenticated(1);

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatar));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ZoneTransfer));

        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateMousePin));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.VerifyMousePin));
    }

    [Fact]
    public void HandoverIssued_ThenFailMoveZone_AllowsRollbackOpcodeOnlyWhileInTransfer()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        session.MarkAuthenticated(1);
        session.MarkCharSelect();

        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ZoneTransferFailure));

        session.MarkHandoverIssued();

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ZoneTransferFailure));
    }
}

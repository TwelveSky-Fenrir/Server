using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Contracts;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests;

/// <summary>
///     The PIN gate state machine introduced between <c>Authenticated</c> and <c>CharSelect</c>
///     (login protocol report §0, <c>P2ndPassword=1</c>): <c>LoginClientSession</c>'s state transitions and the
///     opcode gates that key off them (<c>SessionStateGate</c>, generated from every
///     <c>
///         [FenrirPacket(AllowedStates
///         = [...])]
///     </c>
///     ).
/// </summary>
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

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ClientOkForLoginSend));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateMousePasswordSend));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ChangeMousePasswordSend));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.LoginMousePasswordSend));

        // The char-select opcodes must stay closed until the PIN gate opens (legacy Quit() on !IsSecondLogin()).
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatarSend2));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.DeleteAvatarSend));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ChangeAvatarNameSend));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.WantGiftSend));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.DemandZoneServerInfoSend));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.GiftInfoSend));
    }

    [Fact]
    public void MarkCharSelect_FromPinRequired_OpensTheCharSelectOpcodesAndClosesThePinOnes()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        session.MarkAuthenticated(1);
        session.MarkPinRequired();

        session.MarkCharSelect();

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatarSend2));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ChangeAvatarNameSend));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.WantGiftSend));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.GiftInfoSend));

        // Ops 13/14/15 are only legal while the gate is still closed.
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateMousePasswordSend));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.ChangeMousePasswordSend));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.LoginMousePasswordSend));
    }

    [Fact]
    public void Authenticated_WithoutPinRequired_AlreadyAllowsCharSelectOpcodes()
    {
        // RequireSecondPassword=false path: the session never visits PinRequired at all.
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        session.MarkAuthenticated(1);

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateAvatarSend2));
        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.DemandZoneServerInfoSend));

        // But the mandatory-PIN-only opcodes are not legal here (session never entered PinRequired).
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.CreateMousePasswordSend));
        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.LoginMousePasswordSend));
    }

    [Fact]
    public void HandoverIssued_ThenFailMoveZone_AllowsRollbackOpcodeOnlyWhileInTransfer()
    {
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        session.MarkAuthenticated(1);
        session.MarkCharSelect();

        Assert.False(session.IsOpcodeAllowed(Opcodes.Login.Incoming.FailMoveZone1Send));

        session.MarkHandoverIssued();

        Assert.True(session.IsOpcodeAllowed(Opcodes.Login.Incoming.FailMoveZone1Send));
    }
}

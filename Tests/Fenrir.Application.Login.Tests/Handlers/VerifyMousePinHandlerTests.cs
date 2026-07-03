using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

/// <summary>
///     op15 CL_LOGIN_MOUSE_PASSWORD_SEND — PIN verification (login protocol report §4.15): match opens
///     CharSelect, mismatch replies Result=1 and strikes the counter, the THIRD strike disconnects
///     (<see cref="VerifyMousePinHandler" />'s <c>MaxPinFailures</c>).
/// </summary>
public class ClLoginMousePasswordSendHandlerTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task HandleAsync_CorrectPin_OpensCharSelect()
    {
        var pins = FakeAccountPinRepository.WithPin("4242");
        var handler = new VerifyMousePinHandler(pins);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "4242" }, session,
            CancellationToken.None);

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new VerifyMousePinResponse { Result = 0 });
    }

    [Fact]
    public async Task HandleAsync_NoPinStored_AbortsAsStateViolation()
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        var handler = new VerifyMousePinHandler(pins);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "4242" }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_InvalidFormat_AbortsAsMalformed()
    {
        var pins = FakeAccountPinRepository.WithPin("4242");
        var handler = new VerifyMousePinHandler(pins);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "42x2" }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_WrongPin_TwiceThenThird_StaysUntilThirdStrikeDisconnects()
    {
        var pins = FakeAccountPinRepository.WithPin("4242");
        var handler = new VerifyMousePinHandler(pins);
        var (session, pipe) = CreateSessionInPinRequired();

        // Strikes 1 and 2: replied Result=1, session stays alive and still parked in PinRequired.
        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "0000" }, session,
            CancellationToken.None);
        await PacketAssert.AssertSentAsync(pipe, new VerifyMousePinResponse { Result = 1 });
        Assert.Null(session.DisconnectReason);
        Assert.Equal(LoginSessionState.PinRequired, session.State);

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "0000" }, session,
            CancellationToken.None);
        await PacketAssert.AssertSentAsync(pipe, new VerifyMousePinResponse { Result = 1 });
        Assert.Null(session.DisconnectReason);

        // Strike 3: legacy GL_504 -> Quit(), still replies Result=1 THEN disconnects (S04_MyWork02.cpp l.567-573).
        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "0000" }, session,
            CancellationToken.None);
        await PacketAssert.AssertSentAsync(pipe, new VerifyMousePinResponse { Result = 1 });
        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    private static (LoginClientSession Session, FakeDuplexPipe Pipe) CreateSessionInPinRequired()
    {
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(AccountId);
        session.MarkPinRequired();
        return (session, pipe);
    }
}

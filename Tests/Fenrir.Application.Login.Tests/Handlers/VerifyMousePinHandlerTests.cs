using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Handlers.Services;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

// op15 CL_LOGIN_MOUSE_PASSWORD_SEND -- match opens CharSelect, mismatch replies Result=1 and strikes the
// counter, the third strike disconnects (VerifyMousePinHandler.MaxPinFailures).
public class ClLoginMousePasswordSendHandlerTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task HandleAsync_CorrectPin_OpensCharSelect()
    {
        var pins = FakeAccountPinRepository.WithPin("4242");
        var handler = new VerifyMousePinHandler(new VerifyMousePinService(pins));
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
        var handler = new VerifyMousePinHandler(new VerifyMousePinService(pins));
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
        var handler = new VerifyMousePinHandler(new VerifyMousePinService(pins));
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
        var handler = new VerifyMousePinHandler(new VerifyMousePinService(pins));
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "0000" }, session,
            CancellationToken.None);
        await PacketAssert.AssertSentAsync(pipe, new VerifyMousePinResponse { Result = 1 });
        Assert.Null(session.DisconnectReason);
        Assert.Equal(LoginSessionState.PinRequired, session.State);

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "0000" }, session,
            CancellationToken.None);
        await PacketAssert.AssertSentAsync(pipe, new VerifyMousePinResponse { Result = 1 });
        Assert.Null(session.DisconnectReason);

        // Strike 3: legacy GL_504 -> Quit() (S04_MyWork02.cpp l.567-573), still replies Result=1 then disconnects.
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

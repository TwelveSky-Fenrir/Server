using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

// op13 CL_CREATE_MOUSE_PASSWORD_SEND -- legacy precondition failures all Quit() with no reply; Fenrir maps
// every one to ClientSession.Abort.
public class ClCreateMousePasswordSendHandlerTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task HandleAsync_NoExistingPin_StoresHashedPinAndOpensCharSelect()
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        var handler = new CreateMousePinHandler(pins);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new CreateMousePinRequest { MousePassword = "1234" }, session,
            CancellationToken.None);

        Assert.Equal(1, pins.SetCallCount);
        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new CreateMousePinResponse { Result = 0, MousePassword = "1234" });
    }

    [Fact]
    public async Task HandleAsync_PinAlreadyExists_AbortsWithoutStoring()
    {
        var pins = FakeAccountPinRepository.WithPin("5678");
        var handler = new CreateMousePinHandler(pins);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new CreateMousePinRequest { MousePassword = "1234" }, session,
            CancellationToken.None);

        Assert.Equal(0, pins.SetCallCount);
        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Theory]
    [InlineData("123")] // too short
    [InlineData("12345")] // too long
    [InlineData("12a4")] // non-digit
    [InlineData("")]
    public async Task HandleAsync_InvalidFormat_AbortsAsMalformed(string malformedPin)
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        var handler = new CreateMousePinHandler(pins);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new CreateMousePinRequest { MousePassword = malformedPin }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_StorageFailure_AbortsAsFaulted()
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        pins.ThrowOnSet = true;
        var handler = new CreateMousePinHandler(pins);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new CreateMousePinRequest { MousePassword = "1234" }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
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

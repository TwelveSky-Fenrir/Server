using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.CreateMousePin;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Login.Wire;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Handlers;

public class ClCreateMousePasswordSendHandlerTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task HandleAsync_NoExistingPin_StoresHashedPinAndOpensCharSelect()
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        var handler = new CreateMousePinHandler(
            new CreateMousePinService(pins, NullLogger<CreateMousePinService>.Instance),
            NullLogger<CreateMousePinHandler>.Instance);
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
        var handler = new CreateMousePinHandler(
            new CreateMousePinService(pins, NullLogger<CreateMousePinService>.Instance),
            NullLogger<CreateMousePinHandler>.Instance);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new CreateMousePinRequest { MousePassword = "1234" }, session,
            CancellationToken.None);

        Assert.Equal(0, pins.SetCallCount);
        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_PinAlreadyExistsAndSubmittedFormatAlsoMalformed_AbortsAsStateViolationNotMalformed()
    {
        var pins = FakeAccountPinRepository.WithPin("5678");
        var handler = new CreateMousePinHandler(
            new CreateMousePinService(pins, NullLogger<CreateMousePinService>.Instance),
            NullLogger<CreateMousePinHandler>.Instance);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new CreateMousePinRequest { MousePassword = "12a4" }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        Assert.Equal(0, pins.SetCallCount);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12a4")]
    [InlineData("")]
    public async Task HandleAsync_InvalidFormat_AbortsAsMalformed(string malformedPin)
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        var handler = new CreateMousePinHandler(
            new CreateMousePinService(pins, NullLogger<CreateMousePinService>.Instance),
            NullLogger<CreateMousePinHandler>.Instance);
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
        var handler = new CreateMousePinHandler(
            new CreateMousePinService(pins, NullLogger<CreateMousePinService>.Instance),
            NullLogger<CreateMousePinHandler>.Instance);
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

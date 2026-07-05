using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Handlers.Services;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

// op14 CL_CHANGE_MOUSE_PASSWORD_SEND -- legacy quirk (S04_MyWork02.cpp l.532): success proceeds straight
// to CharSelect.
public class ClChangeMousePasswordSendHandlerTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task HandleAsync_CorrectCurrentPin_StoresNewPinAndOpensCharSelect()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        var handler = new ChangeMousePinHandler(new ChangeMousePinService(pins));
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(
            new ChangeMousePinRequest { MousePassword = "1111", ChangeMousePassword = "2222" }, session,
            CancellationToken.None);

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new ChangeMousePinResponse { Result = 0, MousePassword = "2222" });
    }

    [Fact]
    public async Task HandleAsync_NoPinStored_AbortsAsStateViolation()
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        var handler = new ChangeMousePinHandler(new ChangeMousePinService(pins));
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(
            new ChangeMousePinRequest { MousePassword = "1111", ChangeMousePassword = "2222" }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_InvalidNewFormat_AbortsAsMalformed()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        var handler = new ChangeMousePinHandler(new ChangeMousePinService(pins));
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(
            new ChangeMousePinRequest { MousePassword = "1111", ChangeMousePassword = "22x2" }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_WrongCurrentPin_RepliesResultOneAndStrikes_ThirdDisconnects()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        var handler = new ChangeMousePinHandler(new ChangeMousePinService(pins));
        var (session, pipe) = CreateSessionInPinRequired();
        var attempt = new ChangeMousePinRequest { MousePassword = "0000", ChangeMousePassword = "2222" };

        await handler.HandleAsync(attempt, session, CancellationToken.None);
        await PacketAssert.AssertSentAsync(pipe, new ChangeMousePinResponse { Result = 1, MousePassword = "0000" });
        Assert.Null(session.DisconnectReason);

        await handler.HandleAsync(attempt, session, CancellationToken.None);
        await PacketAssert.AssertSentAsync(pipe, new ChangeMousePinResponse { Result = 1, MousePassword = "0000" });
        Assert.Null(session.DisconnectReason);

        await handler.HandleAsync(attempt, session, CancellationToken.None);
        await PacketAssert.AssertSentAsync(pipe, new ChangeMousePinResponse { Result = 1, MousePassword = "0000" });
        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_StorageFailure_RepliesResultTwoWithoutDisconnecting()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        pins.ThrowOnSet = true;
        var handler = new ChangeMousePinHandler(new ChangeMousePinService(pins));
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(
            new ChangeMousePinRequest { MousePassword = "1111", ChangeMousePassword = "2222" }, session,
            CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new ChangeMousePinResponse { Result = 2, MousePassword = "0000" });
        Assert.Null(session.DisconnectReason);
        Assert.Equal(LoginSessionState.PinRequired, session.State);
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

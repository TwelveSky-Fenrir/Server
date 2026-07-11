using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.VerifyMousePin;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Login.Wire;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Handlers;

public class ClLoginMousePasswordSendHandlerTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task HandleAsync_CorrectPin_OpensCharSelect()
    {
        var pins = FakeAccountPinRepository.WithPin("4242");
        var eventLog = new FakeEventLogRepository();
        var handler = new VerifyMousePinHandler(
            new VerifyMousePinService(pins, eventLog, NullLogger<VerifyMousePinService>.Instance),
            NullLogger<VerifyMousePinHandler>.Instance);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "4242" }, session,
            CancellationToken.None);

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new VerifyMousePinResponse { Result = 0 });
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_NoPinStored_AbortsAsStateViolation()
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        var eventLog = new FakeEventLogRepository();
        var handler = new VerifyMousePinHandler(
            new VerifyMousePinService(pins, eventLog, NullLogger<VerifyMousePinService>.Instance),
            NullLogger<VerifyMousePinHandler>.Instance);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "4242" }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_InvalidFormat_AbortsAsMalformed()
    {
        var pins = FakeAccountPinRepository.WithPin("4242");
        var eventLog = new FakeEventLogRepository();
        var handler = new VerifyMousePinHandler(
            new VerifyMousePinService(pins, eventLog, NullLogger<VerifyMousePinService>.Instance),
            NullLogger<VerifyMousePinHandler>.Instance);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "42x2" }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_WrongPin_TwiceThenThird_StaysUntilThirdStrikeDisconnects()
    {
        var pins = FakeAccountPinRepository.WithPin("4242");
        var eventLog = new FakeEventLogRepository();
        var handler = new VerifyMousePinHandler(
            new VerifyMousePinService(pins, eventLog, NullLogger<VerifyMousePinService>.Instance),
            NullLogger<VerifyMousePinHandler>.Instance);
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

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "0000" }, session,
            CancellationToken.None);
        await PacketAssert.AssertSentAsync(pipe, new VerifyMousePinResponse { Result = 1 });
        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);

        Assert.Equal(3, eventLog.LoggedEvents.Count);
        Assert.All(eventLog.LoggedEvents,
            e =>
            {
                Assert.Equal(EventLogCategory.AccountSecurity, e.Category);
                Assert.Equal(AccountId, e.ActorAccountId);
            });
        Assert.Equal((short)1, eventLog.LoggedEvents[0].EventCode);
        Assert.Equal((byte)1, eventLog.LoggedEvents[0].Outcome);
        Assert.Equal((short)1, eventLog.LoggedEvents[1].EventCode);
        Assert.Equal((byte)2, eventLog.LoggedEvents[1].Outcome);
        Assert.Equal((short)2, eventLog.LoggedEvents[2].EventCode);
        Assert.Equal((byte)3, eventLog.LoggedEvents[2].Outcome);
    }

    [Fact]
    public async Task HandleAsync_AccountScopedLockout_AbortsSilentlyWithoutReplyEvenForACorrectPin()
    {
        var pins = FakeAccountPinRepository.WithLockedPin("4242", DateTime.UtcNow.AddMinutes(1));
        var eventLog = new FakeEventLogRepository();
        var handler = new VerifyMousePinHandler(
            new VerifyMousePinService(pins, eventLog, NullLogger<VerifyMousePinService>.Instance),
            NullLogger<VerifyMousePinHandler>.Instance);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(new VerifyMousePinRequest { MousePasswordInput = "4242" }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)6, logged.EventCode);
        Assert.Equal(EventLogCategory.AccountSecurity, logged.Category);
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

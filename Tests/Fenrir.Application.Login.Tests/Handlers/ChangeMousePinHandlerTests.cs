using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.ChangeMousePin;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Handlers;

// op14 CL_CHANGE_MOUSE_PASSWORD_SEND -- legacy quirk (S04_MyWork02.cpp l.532): success proceeds straight
// to CharSelect. Every mismatch is also recorded as a game.EventLog AccountSecurity row, mirroring op15's
// own audit trail (pincode-second-password security audit's Major finding: closes the asymmetry where an
// attacker could previously brute-force the shared PinFailureCount counter entirely through op14 and
// leave no durable trace).
public class ClChangeMousePasswordSendHandlerTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task HandleAsync_CorrectCurrentPin_StoresNewPinAndOpensCharSelect()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        var eventLog = new FakeEventLogRepository();
        var handler = new ChangeMousePinHandler(
            new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance),
            NullLogger<ChangeMousePinHandler>.Instance);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(
            new ChangeMousePinRequest { MousePassword = "1111", ChangeMousePassword = "2222" }, session,
            CancellationToken.None);

        Assert.Equal(LoginSessionState.CharSelect, session.State);
        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe, new ChangeMousePinResponse { Result = 0, MousePassword = "2222" });
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_NoPinStored_AbortsAsStateViolation()
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        var eventLog = new FakeEventLogRepository();
        var handler = new ChangeMousePinHandler(
            new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance),
            NullLogger<ChangeMousePinHandler>.Instance);
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
        var eventLog = new FakeEventLogRepository();
        var handler = new ChangeMousePinHandler(
            new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance),
            NullLogger<ChangeMousePinHandler>.Instance);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(
            new ChangeMousePinRequest { MousePassword = "1111", ChangeMousePassword = "22x2" }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_WrongCurrentPin_RepliesResultOneAndStrikes_ThirdDisconnects_AndAuditsEveryMismatch()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        var eventLog = new FakeEventLogRepository();
        var handler = new ChangeMousePinHandler(
            new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance),
            NullLogger<ChangeMousePinHandler>.Instance);
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

        // Closes the audit-trail asymmetry: op14 mismatches are now recorded exactly like op15's own
        // VerifyMousePinHandler suite (ClLoginMousePasswordSendHandlerTests.
        // HandleAsync_WrongPin_TwiceThenThird_StaysUntilThirdStrikeDisconnects), just with the distinct
        // MousePinChangeMismatch/MousePinChangeLockout event codes.
        Assert.Equal(3, eventLog.LoggedEvents.Count);
        Assert.All(eventLog.LoggedEvents,
            e =>
            {
                Assert.Equal(EventLogCategory.AccountSecurity, e.Category);
                Assert.Equal(AccountId, e.ActorAccountId);
            });
        Assert.Equal((short)4, eventLog.LoggedEvents[0].EventCode); // MousePinChangeMismatch
        Assert.Equal((byte)1, eventLog.LoggedEvents[0].Outcome);
        Assert.Equal((short)4, eventLog.LoggedEvents[1].EventCode);
        Assert.Equal((byte)2, eventLog.LoggedEvents[1].Outcome);
        Assert.Equal((short)5, eventLog.LoggedEvents[2].EventCode); // MousePinChangeLockout
        Assert.Equal((byte)3, eventLog.LoggedEvents[2].Outcome);
    }

    [Fact]
    public async Task HandleAsync_AccountScopedLockout_AbortsSilentlyWithoutReplyOrDisconnectingViaStrikeCount()
    {
        var pins = FakeAccountPinRepository.WithLockedPin("1111", DateTime.UtcNow.AddMinutes(1));
        var eventLog = new FakeEventLogRepository();
        var handler = new ChangeMousePinHandler(
            new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance),
            NullLogger<ChangeMousePinHandler>.Instance);
        var (session, pipe) = CreateSessionInPinRequired();

        await handler.HandleAsync(
            new ChangeMousePinRequest { MousePassword = "1111", ChangeMousePassword = "2222" }, session,
            CancellationToken.None);

        // Fenrir-only account-scoped lockout (Migrations/028_account_pin_lockout.sql): silent abort, no
        // wire reply -- matching NoPinConfigured/InvalidFormat's posture, unlike WrongPassword's Result=1.
        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Equal(0, pins.SetCallCount);
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)6, logged.EventCode); // MousePinAttemptRejectedLocked
    }

    [Fact]
    public async Task HandleAsync_StorageFailure_RepliesResultTwoWithoutDisconnecting()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        pins.ThrowOnSet = true;
        var eventLog = new FakeEventLogRepository();
        var handler = new ChangeMousePinHandler(
            new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance),
            NullLogger<ChangeMousePinHandler>.Instance);
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

using Fenrir.Application.Login.Abstractions.VerifyMousePin;
using Fenrir.Application.Login.Services.VerifyMousePin;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Microsoft.Extensions.Logging.Abstractions;

using Fenrir.Network.Dispatch.Login.Sessions;

namespace Fenrir.Application.Login.Tests.Services;

// op15 CL_LOGIN_MOUSE_PASSWORD_SEND business logic: the Fenrir-only account-scoped, cross-reconnect PIN
// lockout (Migrations/028_account_pin_lockout.sql) added to close the pincode-second-password security
// audit's Major finding -- LoginClientSession.PinFailureCount alone resets on every reconnect
// (MarkPinRequired), so only a durable, account-scoped counter can actually blunt brute forcing across
// reconnects.
public class VerifyMousePinServiceTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task VerifyMousePinAsync_CorrectPin_RecordsSuccessfulAttempt()
    {
        var pins = FakeAccountPinRepository.WithPin("4242");
        var eventLog = new FakeEventLogRepository();
        var service = new VerifyMousePinService(pins, eventLog, NullLogger<VerifyMousePinService>.Instance);

        var result = await service.VerifyMousePinAsync(AccountId, "4242", CancellationToken.None);

        Assert.Equal(VerifyMousePinOutcome.Success, result.Outcome);
        Assert.Equal(1, pins.RecordAttemptCallCount);
        Assert.Equal(0, pins.Stored!.FailedAttempts);
        Assert.Null(pins.Stored.LockedUntilUtc);
    }

    [Fact]
    public async Task VerifyMousePinAsync_WrongPin_RecordsFailedAttemptButStaysUnderThreshold()
    {
        var pins = FakeAccountPinRepository.WithPin("4242");
        var eventLog = new FakeEventLogRepository();
        var service = new VerifyMousePinService(pins, eventLog, NullLogger<VerifyMousePinService>.Instance);

        var result = await service.VerifyMousePinAsync(AccountId, "0000", CancellationToken.None);

        Assert.Equal(VerifyMousePinOutcome.WrongPassword, result.Outcome);
        Assert.Equal(1, pins.Stored!.FailedAttempts);
        Assert.Null(pins.Stored.LockedUntilUtc);
    }

    [Fact]
    public async Task VerifyMousePinAsync_FifthCumulativeMismatchAcrossReconnects_LocksTheAccount()
    {
        // Simulates 5 wrong attempts spread across what would be several reconnects at the wire level
        // (each reconnect resets LoginClientSession.PinFailureCount to 0 via MarkPinRequired, but never
        // touches this account-scoped counter) -- the exact scenario the audit's Major finding describes:
        // an attacker who never lets a single session reach the 3-strike disconnect can still be stopped
        // by the durable counter.
        var pins = FakeAccountPinRepository.WithPin("4242");
        var eventLog = new FakeEventLogRepository();
        var service = new VerifyMousePinService(pins, eventLog, NullLogger<VerifyMousePinService>.Instance);

        for (var i = 0; i < 4; i++)
            await service.VerifyMousePinAsync(AccountId, "0000", CancellationToken.None);
        Assert.Null(pins.Stored!.LockedUntilUtc);

        await service.VerifyMousePinAsync(AccountId, "0000", CancellationToken.None);

        Assert.NotNull(pins.Stored!.LockedUntilUtc);
        Assert.True(pins.Stored.LockedUntilUtc > DateTime.UtcNow);
        Assert.Equal(5, pins.Stored.FailedAttempts);
    }

    [Fact]
    public async Task VerifyMousePinAsync_AccountLocked_RejectsEvenACorrectPinWithoutTouchingHash()
    {
        var pins = FakeAccountPinRepository.WithLockedPin("4242", DateTime.UtcNow.AddMinutes(1));
        var eventLog = new FakeEventLogRepository();
        var service = new VerifyMousePinService(pins, eventLog, NullLogger<VerifyMousePinService>.Instance);

        // Correct PIN, but the account-scoped lockout must short-circuit before PasswordHasher.Verify --
        // otherwise a locked-out account could still be probed for the right answer.
        var result = await service.VerifyMousePinAsync(AccountId, "4242", CancellationToken.None);

        Assert.Equal(VerifyMousePinOutcome.Locked, result.Outcome);
        // A rejected-before-comparison outcome never reports to the lockout counter itself.
        Assert.Equal(0, pins.RecordAttemptCallCount);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)6, logged.EventCode); // MousePinAttemptRejectedLocked
        Assert.Equal(EventLogCategory.AccountSecurity, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
    }

    [Fact]
    public async Task VerifyMousePinAsync_CorrectPinAfterPriorMismatches_ResetsTheAccountScopedCounter()
    {
        var pins = FakeAccountPinRepository.WithPin("4242");
        var eventLog = new FakeEventLogRepository();
        var service = new VerifyMousePinService(pins, eventLog, NullLogger<VerifyMousePinService>.Instance);

        await service.VerifyMousePinAsync(AccountId, "0000", CancellationToken.None);
        await service.VerifyMousePinAsync(AccountId, "0000", CancellationToken.None);
        Assert.Equal(2, pins.Stored!.FailedAttempts);

        var result = await service.VerifyMousePinAsync(AccountId, "4242", CancellationToken.None);

        Assert.Equal(VerifyMousePinOutcome.Success, result.Outcome);
        Assert.Equal(0, pins.Stored!.FailedAttempts);
        Assert.Null(pins.Stored.LockedUntilUtc);
    }
}

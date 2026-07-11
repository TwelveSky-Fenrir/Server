using Fenrir.Application.Login.Abstractions.VerifyMousePin;
using Fenrir.Application.Login.Services.VerifyMousePin;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Services;

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

        var result = await service.VerifyMousePinAsync(AccountId, "4242", CancellationToken.None);

        Assert.Equal(VerifyMousePinOutcome.Locked, result.Outcome);
        Assert.Equal(0, pins.RecordAttemptCallCount);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)6, logged.EventCode);
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

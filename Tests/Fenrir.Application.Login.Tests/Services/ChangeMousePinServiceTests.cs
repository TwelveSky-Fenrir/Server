using Fenrir.Application.Login.Abstractions.ChangeMousePin;
using Fenrir.Application.Login.Services.ChangeMousePin;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Services;

// op14 CL_CHANGE_MOUSE_PASSWORD_SEND business logic: shares LoginClientSession.PinFailureCount and the
// account-scoped lockout (Migrations/028_account_pin_lockout.sql) with VerifyMousePinService -- see
// VerifyMousePinServiceTests for the sibling coverage of the same mechanism through op15.
public class ChangeMousePinServiceTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task ChangeMousePinAsync_CorrectCurrentPin_RecordsSuccessAndResetsCounter()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        var eventLog = new FakeEventLogRepository();
        var service = new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance);

        var result = await service.ChangeMousePinAsync(AccountId, "1111", "2222", CancellationToken.None);

        Assert.Equal(ChangeMousePinOutcome.Success, result.Outcome);
        Assert.Equal(0, pins.Stored!.FailedAttempts);
        Assert.Null(pins.Stored.LockedUntilUtc);
    }

    [Fact]
    public async Task ChangeMousePinAsync_WrongCurrentPin_RecordsFailureAndLogsMismatch()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        var eventLog = new FakeEventLogRepository();
        var service = new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance);

        var result = await service.ChangeMousePinAsync(AccountId, "0000", "2222", CancellationToken.None);

        Assert.Equal(ChangeMousePinOutcome.WrongPassword, result.Outcome);
        Assert.Equal(1, pins.Stored!.FailedAttempts);

        // LogFailedAttemptAsync is called by the handler, not the service, so no event is logged yet here
        // -- mirrors VerifyMousePinService's own split between VerifyMousePinAsync (DB-only accounting)
        // and LogFailedAttemptAsync (audit trail, driven by the handler's session-scoped strike count).
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task ChangeMousePinAsync_FifthCumulativeMismatchAcrossReconnects_LocksTheAccount()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        var eventLog = new FakeEventLogRepository();
        var service = new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance);

        for (var i = 0; i < 4; i++)
            await service.ChangeMousePinAsync(AccountId, "0000", "2222", CancellationToken.None);
        Assert.Null(pins.Stored!.LockedUntilUtc);

        await service.ChangeMousePinAsync(AccountId, "0000", "2222", CancellationToken.None);

        Assert.NotNull(pins.Stored!.LockedUntilUtc);
        Assert.Equal(5, pins.Stored.FailedAttempts);
    }

    [Fact]
    public async Task ChangeMousePinAsync_AccountLocked_RejectsEvenACorrectCurrentPinWithoutTouchingHash()
    {
        var pins = FakeAccountPinRepository.WithLockedPin("1111", DateTime.UtcNow.AddMinutes(1));
        var eventLog = new FakeEventLogRepository();
        var service = new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance);

        var result = await service.ChangeMousePinAsync(AccountId, "1111", "2222", CancellationToken.None);

        Assert.Equal(ChangeMousePinOutcome.Locked, result.Outcome);
        Assert.Equal(0, pins.RecordAttemptCallCount);
        Assert.Equal(0, pins.SetCallCount);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)6, logged.EventCode); // MousePinAttemptRejectedLocked, shared with VerifyMousePinService
        Assert.Equal(EventLogCategory.AccountSecurity, logged.Category);
    }

    [Fact]
    public async Task LogFailedAttemptAsync_MirrorsVerifyMousePinServiceButWithDistinctChangeEventCodes()
    {
        var pins = FakeAccountPinRepository.WithPin("1111");
        var eventLog = new FakeEventLogRepository();
        var service = new ChangeMousePinService(pins, eventLog, NullLogger<ChangeMousePinService>.Instance);

        await service.LogFailedAttemptAsync(AccountId, 1, false, CancellationToken.None);
        await service.LogFailedAttemptAsync(AccountId, 3, true, CancellationToken.None);

        Assert.Equal(2, eventLog.LoggedEvents.Count);
        Assert.Equal((short)4, eventLog.LoggedEvents[0].EventCode); // MousePinChangeMismatch
        Assert.Equal((byte)1, eventLog.LoggedEvents[0].Outcome);
        Assert.Equal((short)5, eventLog.LoggedEvents[1].EventCode); // MousePinChangeLockout
        Assert.Equal((byte)3, eventLog.LoggedEvents[1].Outcome);
        Assert.All(eventLog.LoggedEvents, e => Assert.Equal(EventLogCategory.AccountSecurity, e.Category));
    }
}

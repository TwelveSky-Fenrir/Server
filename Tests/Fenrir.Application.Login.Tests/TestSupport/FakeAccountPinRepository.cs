using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Security;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for IAccountPinRepository so the PIN handlers are unit-testable without a SQL
// container. Single-account: every PIN handler only ever touches its own session's account.
internal sealed class FakeAccountPinRepository : IAccountPinRepository
{
    /// <summary>Throws from <see cref="SetAsync" /> to simulate the legacy UpdateMousePassword storage failure.</summary>
    public bool ThrowOnSet { get; set; }

    public int SetCallCount { get; private set; }

    /// <summary>Number of <see cref="RecordAttemptAsync" /> calls observed, for assertions.</summary>
    public int RecordAttemptCallCount { get; private set; }

    /// <summary>Current row state, for assertions that need to inspect FailedAttempts/LockedUntilUtc directly.</summary>
    public AccountPinDto? Stored { get; private set; }

    public ValueTask<AccountPinDto?> GetAsync(int accountId, CancellationToken ct)
    {
        return ValueTask.FromResult(Stored);
    }

    public ValueTask SetAsync(int accountId, byte[] pinHash, byte[] pinSalt, CancellationToken ct)
    {
        if (ThrowOnSet)
            throw new InvalidOperationException("Simulated auth.usp_AccountPin_Set failure.");

        SetCallCount++;
        // A fresh PIN write also resets the account-scoped lockout counter (usp_AccountPin_Set itself
        // does not touch these columns; only CreateMousePinService ever hits this branch with a fresh
        // account, where FailedAttempts is already 0, so no explicit reset needed here beyond the
        // implicit one from constructing a brand-new AccountPinDto).
        Stored = new AccountPinDto(pinHash, pinSalt);
        return ValueTask.CompletedTask;
    }

    // Mirrors auth.usp_AccountPin_RecordAttempt's progressive-lockout thresholds exactly (Migrations/
    // 028_account_pin_lockout.sql): >=10 cumulative failures -> 15 min lock, >=5 -> 1 min lock, success
    // resets both counters.
    public ValueTask RecordAttemptAsync(int accountId, bool success, CancellationToken ct)
    {
        RecordAttemptCallCount++;

        if (Stored is null)
            return ValueTask.CompletedTask;

        if (success)
        {
            Stored = Stored with { FailedAttempts = 0, LockedUntilUtc = null };
            return ValueTask.CompletedTask;
        }

        var failedAttempts = Stored.FailedAttempts + 1;
        var lockedUntil = failedAttempts switch
        {
            >= 10 => DateTime.UtcNow.AddMinutes(15),
            >= 5 => DateTime.UtcNow.AddMinutes(1),
            _ => Stored.LockedUntilUtc
        };
        Stored = Stored with { FailedAttempts = failedAttempts, LockedUntilUtc = lockedUntil };
        return ValueTask.CompletedTask;
    }

    public static FakeAccountPinRepository WithNoPin()
    {
        return new FakeAccountPinRepository();
    }

    public static FakeAccountPinRepository WithPin(string pin)
    {
        var (hash, salt) = PasswordHasher.Hash(pin);
        return new FakeAccountPinRepository { Stored = new AccountPinDto(hash, salt) };
    }

    /// <summary>Seeds a PIN whose account-scoped lockout is already active, for Locked-outcome tests.</summary>
    public static FakeAccountPinRepository WithLockedPin(string pin, DateTime lockedUntilUtc)
    {
        var (hash, salt) = PasswordHasher.Hash(pin);
        return new FakeAccountPinRepository
        {
            Stored = new AccountPinDto(hash, salt, 5, lockedUntilUtc)
        };
    }
}

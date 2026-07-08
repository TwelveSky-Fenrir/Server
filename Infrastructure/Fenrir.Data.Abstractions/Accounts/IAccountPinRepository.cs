namespace Fenrir.Data.Abstractions.Accounts;

// Interface (unlike AccountRepository) so the PIN state machine can be unit-tested via a fake, without a SQL container.
public interface IAccountPinRepository
{
    /// <summary>Null when the account has no PIN yet.</summary>
    public ValueTask<AccountPinDto?> GetAsync(int accountId, CancellationToken ct);

    /// <summary>Upsert (auth.usp_AccountPin_Set), mirrors the legacy UpdateMousePassword.</summary>
    public ValueTask SetAsync(int accountId, byte[] pinHash, byte[] pinSalt, CancellationToken ct);

    /// <summary>
    ///     Records one real mouse-PIN comparison attempt against the account-scoped, cross-reconnect
    ///     lockout counter (auth.usp_AccountPin_RecordAttempt, Migrations/028_account_pin_lockout.sql) --
    ///     the Fenrir-only analog of <see cref="IAccountRepository.RecordLoginAttemptAsync" /> for the
    ///     primary password. Callers must invoke this exactly once per real
    ///     <see cref="Fenrir.Data.Security.PasswordHasher.Verify" /> comparison that actually ran (success
    ///     or failure) -- never for an outcome rejected before that comparison (already-locked account,
    ///     malformed PIN, no PIN configured).
    /// </summary>
    public ValueTask RecordAttemptAsync(int accountId, bool success, CancellationToken ct);
}

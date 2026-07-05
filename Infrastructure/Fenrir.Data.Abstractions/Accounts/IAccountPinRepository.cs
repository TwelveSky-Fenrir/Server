namespace Fenrir.Data.Abstractions.Accounts;

// Interface (unlike AccountRepository) so the PIN state machine can be unit-tested via a fake, without a SQL container.
public interface IAccountPinRepository
{
    /// <summary>Null when the account has no PIN yet.</summary>
    public ValueTask<AccountPinDto?> GetAsync(int accountId, CancellationToken ct);

    /// <summary>Upsert (auth.usp_AccountPin_Set), mirrors the legacy UpdateMousePassword.</summary>
    public ValueTask SetAsync(int accountId, byte[] pinHash, byte[] pinSalt, CancellationToken ct);
}

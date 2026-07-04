namespace Fenrir.Data.Accounts;

/// <summary>Abstraction over Fenrir.Data.Accounts.AccountRepository for DI/testability.</summary>
public interface IAccountRepository
{
    public ValueTask<AuthenticateAccountDto?> AuthenticateAsync(string loginName, CancellationToken ct);

    public ValueTask<int> CreateAsync(string loginName, byte[] passwordHash, byte[] passwordSalt,
        CancellationToken ct);

    public ValueTask RecordLoginAttemptAsync(int accountId, bool success, CancellationToken ct);
}

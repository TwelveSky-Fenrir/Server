namespace Fenrir.Data.Accounts;

public interface IAccountRepository
{
    public ValueTask<AuthenticateAccountDto?> AuthenticateAsync(string loginName, CancellationToken ct);

    public ValueTask<int> CreateAsync(string loginName, byte[] passwordHash, byte[] passwordSalt,
        CancellationToken ct);

    public ValueTask RecordLoginAttemptAsync(int accountId, bool success, CancellationToken ct);
}

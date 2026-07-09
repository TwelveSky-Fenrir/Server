namespace Fenrir.Data.Abstractions.Accounts;

public interface IAccountRepository
{
    public ValueTask<AuthenticateAccountDto?> AuthenticateAsync(string loginName, CancellationToken ct);

    public ValueTask<int> CreateAsync(string loginName, byte[] passwordHash, byte[] passwordSalt,
        CancellationToken ct);

    public ValueTask RecordLoginAttemptAsync(int accountId, bool success, CancellationToken ct);

    /// <summary>Sets AccountGrade (legacy uUserSort) to an absolute value; throws if <paramref name="loginName" /> does not exist.</summary>
    public ValueTask SetGradeAsync(string loginName, short accountGrade, CancellationToken ct);
}

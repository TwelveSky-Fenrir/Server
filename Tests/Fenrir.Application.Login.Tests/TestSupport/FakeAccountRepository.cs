using Fenrir.Data.Abstractions.Accounts;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for IAccountRepository: single-account, mirrors FakeAccountPinRepository's scope.
internal sealed class FakeAccountRepository : IAccountRepository
{
    private readonly AuthenticateAccountDto? _account;

    private FakeAccountRepository(AuthenticateAccountDto? account)
    {
        _account = account;
    }

    public (int AccountId, bool Success)? LastRecordedAttempt { get; private set; }
    public int AuthenticateCallCount { get; private set; }

    public ValueTask<AuthenticateAccountDto?> AuthenticateAsync(string loginName, CancellationToken ct)
    {
        AuthenticateCallCount++;
        return ValueTask.FromResult(_account);
    }

    public ValueTask<int> CreateAsync(string loginName, byte[] passwordHash, byte[] passwordSalt, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask RecordLoginAttemptAsync(int accountId, bool success, CancellationToken ct)
    {
        LastRecordedAttempt = (accountId, success);
        return ValueTask.CompletedTask;
    }

    public static FakeAccountRepository WithAccount(AuthenticateAccountDto account)
    {
        return new FakeAccountRepository(account);
    }

    public static FakeAccountRepository WithNoAccount()
    {
        return new FakeAccountRepository(null);
    }
}

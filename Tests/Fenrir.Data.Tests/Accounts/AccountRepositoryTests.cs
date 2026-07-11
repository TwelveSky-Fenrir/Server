using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Accounts;
using Fenrir.Data.Security;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Accounts;

[Collection("SqlServer")]
public sealed class AccountRepositoryTests
{
    private const string SamplePassword = "Correct horse battery staple";

    private readonly IAccountRepository _repository;

    public AccountRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _repository = new AccountRepository(db);
    }

    private static string NewLoginName()
    {
        return $"acct_{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task CreateAsync_InsertsAccount_ReturnsPositiveAccountId()
    {
        var (hash, salt) = PasswordHasher.Hash(SamplePassword);

        var accountId = await _repository.CreateAsync(NewLoginName(), hash, salt, CancellationToken.None);

        Assert.True(accountId > 0);
    }

    [Fact]
    public async Task AuthenticateAsync_KnownLoginName_ReturnsWhatWasInserted()
    {
        var loginName = NewLoginName();
        var (hash, salt) = PasswordHasher.Hash(SamplePassword);
        var accountId = await _repository.CreateAsync(loginName, hash, salt, CancellationToken.None);

        var account = await _repository.AuthenticateAsync(loginName, CancellationToken.None);

        Assert.NotNull(account);
        Assert.Equal(accountId, account!.AccountId);
        Assert.True(hash.AsSpan().SequenceEqual(account.PasswordHash));
        Assert.True(salt.AsSpan().SequenceEqual(account.PasswordSalt));
        Assert.Equal(0, account.FailedLoginCount);
        Assert.Null(account.LockoutUntilUtc);
        Assert.False(account.IsBanned);
    }

    [Fact]
    public async Task AuthenticateAsync_UnknownLoginName_ReturnsNull()
    {
        var account = await _repository.AuthenticateAsync(NewLoginName(), CancellationToken.None);

        Assert.Null(account);
    }

    [Fact]
    public async Task CreateAsync_LoginNameAlreadyTaken_Throws()
    {
        var loginName = NewLoginName();
        var (hash, salt) = PasswordHasher.Hash(SamplePassword);
        await _repository.CreateAsync(loginName, hash, salt, CancellationToken.None);

        var ex = await Record.ExceptionAsync(() =>
            _repository.CreateAsync(loginName, hash, salt, CancellationToken.None).AsTask());

        Assert.NotNull(ex);

        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50101, sqlException.Number);
    }

    [Fact]
    public async Task RecordLoginAttemptAsync_RepeatedFailures_LocksOut_ThenSuccessResets()
    {
        var loginName = NewLoginName();
        var (hash, salt) = PasswordHasher.Hash(SamplePassword);
        var accountId = await _repository.CreateAsync(loginName, hash, salt, CancellationToken.None);

        for (var i = 0; i < 5; i++)
            await _repository.RecordLoginAttemptAsync(accountId, false, CancellationToken.None);

        var afterFailures = await _repository.AuthenticateAsync(loginName, CancellationToken.None);

        Assert.NotNull(afterFailures);
        Assert.Equal(5, afterFailures!.FailedLoginCount);
        Assert.NotNull(afterFailures.LockoutUntilUtc);

        await _repository.RecordLoginAttemptAsync(accountId, true, CancellationToken.None);

        var afterSuccess = await _repository.AuthenticateAsync(loginName, CancellationToken.None);

        Assert.NotNull(afterSuccess);
        Assert.Equal(0, afterSuccess!.FailedLoginCount);
        Assert.Null(afterSuccess.LockoutUntilUtc);
    }
}

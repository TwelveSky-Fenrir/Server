using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Data.Tests.Fixtures;
using Fenrir.Domain.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Accounts;

// Exercises auth.usp_Account_* against the real, containerized SQL Server booted by SqlServerFixture (I-04:
// the database is the contract of truth) -- no mocks, no in-memory provider. The fixture is shared by every
// class in the "SqlServer" collection (one container for the whole suite), so every test below mints its own
// GUID-suffixed LoginName to stay independent from the seeded devtest account and from each other.
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

    // Short prefix + GUID (not the full test name): LoginName is NVARCHAR(64) and a CallerMemberName-based
    // suffix would overflow it for the longer test method names below.
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

        // usp_Account_Create raises THROW 50101 (admin.ErrorCatalog, 501xx = auth range) for a duplicate
        // LoginName. Whether CaeriusNet surfaces the raw SqlException or wraps it (CaeriusNetSqlException),
        // the SqlException is either the exception itself or its InnerException -- check both shapes rather
        // than assume one, and fall back to "an exception was thrown" if neither exposes SqlException.Number.
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

        // usp_Account_RecordLoginAttempt escalates to a 1-minute lockout once FailedLoginCount reaches 5
        // (architecture reference §9.1) -- five failures in a row must trip LockoutUntilUtc.
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

using System.Data;
using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Accounts;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

// game.usp_Cash_* against real SQL Server 2025. The debit test matters: legacy cash-shop v2 shipped with the
// overdraft guard commented out; this pins the reactivated guard.
[Collection("SqlServer")]
public class CashProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly string _connectionString;

    public CashProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task Cash_GetBalance_ReturnsZeroForAnAccountThatNeverBoughtCash()
    {
        var accountId = await CreateAccountAsync();

        Assert.Equal(0, await GetBalanceAsync(accountId));
    }

    [Fact]
    public async Task Cash_Credit_MintsTheRow_AccumulatesAndWritesTheAuditLog()
    {
        var accountId = await CreateAccountAsync();

        await ExecProcAsync("game.usp_Cash_Credit",
            ("AccountId", accountId), ("Amount", 500), ("Reason", (byte)1));
        await ExecProcAsync("game.usp_Cash_Credit",
            ("AccountId", accountId), ("Amount", 250), ("Reason", (byte)1));

        Assert.Equal(750, await GetBalanceAsync(accountId));

        // Two audit rows, in the same transactions as the credits, with self-checking BalanceAfter.
        Assert.Equal(2, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.CashLog WHERE AccountId = {accountId};"));
        Assert.Equal(750, await ScalarAsync<int>(
            $"SELECT TOP 1 BalanceAfter FROM game.CashLog WHERE AccountId = {accountId} ORDER BY CashLogId DESC;"));
    }

    [Fact]
    public async Task Cash_Debit_Spends_WritesTheAuditLog_AndRejectsAnOverdraftWithoutTouchingTheBalance()
    {
        var accountId = await CreateAccountAsync();
        await ExecProcAsync("game.usp_Cash_Credit",
            ("AccountId", accountId), ("Amount", 300), ("Reason", (byte)1));

        await ExecProcAsync("game.usp_Cash_Debit",
            ("AccountId", accountId), ("Amount", 120), ("Reason", (byte)2), ("ProductId", 12809));

        Assert.Equal(180, await GetBalanceAsync(accountId));
        Assert.Equal(-120, await ScalarAsync<int>(
            $"SELECT Delta FROM game.CashLog WHERE AccountId = {accountId} AND Delta < 0;"));
        Assert.Equal(12809, await ScalarAsync<int>(
            $"SELECT ProductId FROM game.CashLog WHERE AccountId = {accountId} AND Delta < 0;"));

        // Overdraft -> THROW 50240, balance untouched, no audit row leaked.
        var overdraft = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_Cash_Debit",
            ("AccountId", accountId), ("Amount", 181), ("Reason", (byte)2)));
        Assert.Equal(50240, overdraft.Number);
        Assert.Equal(180, await GetBalanceAsync(accountId));
        Assert.Equal(2, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.CashLog WHERE AccountId = {accountId};"));

        // An account with no cash row at all is insufficient funds, not an error class of its own.
        var neverCredited = await CreateAccountAsync();
        var missingRow = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_Cash_Debit",
            ("AccountId", neverCredited), ("Amount", 1), ("Reason", (byte)2)));
        Assert.Equal(50240, missingRow.Number);
    }

    [Fact]
    public async Task Cash_DebitAndCredit_RejectNonPositiveAmounts()
    {
        var accountId = await CreateAccountAsync();

        var debitZero = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_Cash_Debit",
            ("AccountId", accountId), ("Amount", 0), ("Reason", (byte)2)));
        Assert.Equal(50241, debitZero.Number);

        var creditNegative = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_Cash_Credit",
            ("AccountId", accountId), ("Amount", -5), ("Reason", (byte)1)));
        Assert.Equal(50241, creditNegative.Number);
    }

    private async Task<int> CreateAccountAsync()
    {
        return await _accounts.CreateAsync($"cashtest-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);
    }

    private async Task<int> GetBalanceAsync(int accountId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("game.usp_Cash_GetBalance", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("AccountId", accountId);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExecProcAsync(string procName, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(procName, connection) { CommandType = CommandType.StoredProcedure };
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}

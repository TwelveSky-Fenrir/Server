using System.Data;
using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

[Collection("SqlServer")]
public class CashProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICashRepository _cash;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;

    public CashProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _cash = new CashRepository(db);
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

        Assert.Equal(2, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.CashLog WHERE AccountId = {accountId};"));
        Assert.Equal(750, await ScalarAsync<int>(
            $"SELECT TOP 1 BalanceAfter FROM game.CashLog WHERE AccountId = {accountId} ORDER BY CashLogId DESC;"));
    }

    // usp_Cash_Debit (a bare single-purpose debit with no item grant) was removed as dead code in the
    // 2026-07-12 Database/ cleanup pass -- every real debit path grants an item, so these tests now route
    // through its live successor, usp_Cash_DebitAndGrantItem, via ICashRepository.DebitAndGrantItemAsync.
    [Fact]
    public async Task Cash_DebitAndGrantItem_Spends_WritesTheAuditLog_AndRejectsAnOverdraftWithoutTouchingTheBalance()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);
        var itemId = await MinItemIdAsync();
        await ExecProcAsync("game.usp_Cash_Credit",
            ("AccountId", accountId), ("Amount", 300), ("Reason", (byte)1));

        await _cash.DebitAndGrantItemAsync(accountId, 120, 2, 12809, characterId, 0,
            [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)], CancellationToken.None);

        Assert.Equal(180, await GetBalanceAsync(accountId));
        Assert.Equal(-120, await ScalarAsync<int>(
            $"SELECT Delta FROM game.CashLog WHERE AccountId = {accountId} AND Delta < 0;"));
        Assert.Equal(12809, await ScalarAsync<int>(
            $"SELECT ProductId FROM game.CashLog WHERE AccountId = {accountId} AND Delta < 0;"));

        var overdraftEx = await Record.ExceptionAsync(() => _cash.DebitAndGrantItemAsync(accountId, 181, 2,
            12809, characterId, 0, [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2)],
            CancellationToken.None).AsTask());
        Assert.NotNull(overdraftEx);
        var overdraftSqlEx = overdraftEx as SqlException ?? overdraftEx.InnerException as SqlException;
        if (overdraftSqlEx is not null)
            Assert.Equal(50240, overdraftSqlEx.Number);
        Assert.Equal(180, await GetBalanceAsync(accountId));
        Assert.Equal(2, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.CashLog WHERE AccountId = {accountId};"));

        var neverCredited = await CreateAccountAsync();
        var neverCreditedCharacterId = await CreateCharacterAsync(neverCredited);
        var missingRowEx = await Record.ExceptionAsync(() => _cash.DebitAndGrantItemAsync(neverCredited, 1, 2,
            12809, neverCreditedCharacterId, 0, [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)],
            CancellationToken.None).AsTask());
        Assert.NotNull(missingRowEx);
        var missingRowSqlEx = missingRowEx as SqlException ?? missingRowEx.InnerException as SqlException;
        if (missingRowSqlEx is not null)
            Assert.Equal(50240, missingRowSqlEx.Number);
    }

    [Fact]
    public async Task Cash_DebitAndGrantItem_WithAuditParams_NestsAnEventLogRowInTheSameTransaction()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);
        var itemId = await MinItemIdAsync();
        await ExecProcAsync("game.usp_Cash_Credit",
            ("AccountId", accountId), ("Amount", 300), ("Reason", (byte)1));

        await _cash.DebitAndGrantItemAsync(accountId, 120, 2, 12809, characterId, 0,
            [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 7)], CancellationToken.None,
            itemId, 1, 7);

        Assert.Equal(1, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.EventLog WHERE ActorAccountId = {accountId} AND ActorCharacterId = {characterId} AND Category = 22;"));
        Assert.Equal(itemId, await ScalarAsync<int>(
            $"SELECT ItemId FROM game.EventLog WHERE ActorAccountId = {accountId} AND Category = 22;"));

        await _cash.DebitAndGrantItemAsync(accountId, 60, 2, 12809, characterId, 0,
            [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 8)], CancellationToken.None);

        Assert.Equal(1, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.EventLog WHERE ActorAccountId = {accountId} AND Category = 22;"));
    }

    [Fact]
    public async Task Cash_DebitAndCredit_RejectNonPositiveAmounts()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);
        var itemId = await MinItemIdAsync();

        var debitZeroEx = await Record.ExceptionAsync(() => _cash.DebitAndGrantItemAsync(accountId, 0, 2, 12809,
            characterId, 0, [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)],
            CancellationToken.None).AsTask());
        Assert.NotNull(debitZeroEx);
        var debitZeroSqlEx = debitZeroEx as SqlException ?? debitZeroEx.InnerException as SqlException;
        if (debitZeroSqlEx is not null)
            Assert.Equal(50241, debitZeroSqlEx.Number);

        var creditNegative = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_Cash_Credit",
            ("AccountId", accountId), ("Amount", -5), ("Reason", (byte)1)));
        Assert.Equal(50241, creditNegative.Number);
    }

    private Task<int> CreateCharacterAsync(int accountId)
    {
        var name = $"T{Guid.NewGuid():N}"[..8];
        return _characters.CreateAsync(accountId, 0, name, 1, 0, 1, 1, 1, 0f, 0f, 0f, 100, 100, 50, 50,
            CancellationToken.None).AsTask();
    }

    private Task<int> MinItemIdAsync()
    {
        return ScalarAsync<int>("SELECT MIN(ItemId) FROM world.Items;");
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

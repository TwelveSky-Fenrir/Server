using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Tribes;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Tests.Fixtures;
using Fenrir.Data.Tribes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

[Collection("SqlServer")]
public class TribeBankDepositProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;
    private readonly ITribeRepository _tribes;

    public TribeBankDepositProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _tribes = new TribeRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task DepositBankAsync_MovesAllOfTheCharactersMoney_IntoTheSlot()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync();
        await SeedBankSlotAsync(tribeId, 3, 10_000);
        await SeedMoneyAsync(characterId, 7_500);

        var newMoney = await _tribes.DepositBankAsync(tribeId, 3, characterId, CancellationToken.None);

        Assert.Equal(0, newMoney);
        Assert.Equal(17_500, await ScalarAsync<int>(
            $"SELECT Amount FROM game.TribeBank WHERE TribeId = {tribeId} AND SlotIndex = 3;"));
        Assert.Equal(0L, await ScalarAsync<long>(
            $"SELECT Money FROM game.Characters WHERE CharacterId = {characterId};"));
    }

    [Fact]
    public async Task DepositBankAsync_CreatesTheSlotWhenItDidNotExistYet()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync();
        await SeedMoneyAsync(characterId, 1_234);

        await _tribes.DepositBankAsync(tribeId, 11, characterId, CancellationToken.None);

        Assert.Equal(1_234, await ScalarAsync<int>(
            $"SELECT Amount FROM game.TribeBank WHERE TribeId = {tribeId} AND SlotIndex = 11;"));
    }

    [Fact]
    public async Task DepositBankAsync_NoMoney_ThrowsAndChangesNothing()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync();
        await SeedBankSlotAsync(tribeId, 5, 999);

        await AssertSqlErrorAsync(50212,
            () => _tribes.DepositBankAsync(tribeId, 5, characterId, CancellationToken.None).AsTask());

        Assert.Equal(999, await ScalarAsync<int>(
            $"SELECT Amount FROM game.TribeBank WHERE TribeId = {tribeId} AND SlotIndex = 5;"));
    }

    [Fact]
    public async Task DepositBankAsync_WritesATribeBankLogRow_WithAPositiveDelta()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync();
        await SeedMoneyAsync(characterId, 4_200);

        await _tribes.DepositBankAsync(tribeId, 8, characterId, CancellationToken.None);

        Assert.Equal(4_200, await ScalarAsync<int>(
            "SELECT Delta FROM game.TribeBankLog WHERE TribeId = " + tribeId +
            $" AND SlotIndex = 8 AND CharacterId = {characterId};"));
        Assert.Equal(4_200, await ScalarAsync<int>(
            "SELECT BalanceAfter FROM game.TribeBankLog WHERE TribeId = " + tribeId +
            $" AND SlotIndex = 8 AND CharacterId = {characterId};"));
    }

    [Fact]
    public async Task WithdrawBankAsync_WritesATribeBankLogRow_WithANegativeDeltaAndZeroBalanceAfter()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync();
        await SeedBankSlotAsync(tribeId, 6, 15_000);
        await SeedMoneyAsync(characterId, 0);

        await _tribes.WithdrawBankAsync(tribeId, 6, characterId, CancellationToken.None);

        Assert.Equal(-15_000, await ScalarAsync<int>(
            "SELECT Delta FROM game.TribeBankLog WHERE TribeId = " + tribeId +
            $" AND SlotIndex = 6 AND CharacterId = {characterId};"));
        Assert.Equal(0, await ScalarAsync<int>(
            "SELECT BalanceAfter FROM game.TribeBankLog WHERE TribeId = " + tribeId +
            $" AND SlotIndex = 6 AND CharacterId = {characterId};"));
    }

    private async Task<byte> CreateTribeAsync()
    {
        const byte tribeId = 1;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "IF NOT EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = @TribeId) " +
            "INSERT INTO game.Tribes (TribeId) VALUES (@TribeId);", connection);
        command.Parameters.AddWithValue("TribeId", tribeId);
        await command.ExecuteNonQueryAsync();

        return tribeId;
    }

    private async Task<int> CreateCharacterAsync()
    {
        var accountId = await _accounts.CreateAsync($"tribebankdeposittest-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"D{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
    }

    private async Task SeedBankSlotAsync(byte tribeId, byte slotIndex, int amount)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO game.TribeBank (TribeId, SlotIndex, Amount) VALUES (@TribeId, @SlotIndex, @Amount);",
            connection);
        command.Parameters.AddWithValue("TribeId", tribeId);
        command.Parameters.AddWithValue("SlotIndex", slotIndex);
        command.Parameters.AddWithValue("Amount", amount);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedMoneyAsync(int characterId, long money)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command =
            new SqlCommand("UPDATE game.Characters SET Money = @Money WHERE CharacterId = @CharacterId;",
                connection);
        command.Parameters.AddWithValue("Money", money);
        command.Parameters.AddWithValue("CharacterId", characterId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task AssertSqlErrorAsync(int expectedNumber, Func<Task> action)
    {
        var thrown = await Record.ExceptionAsync(action);
        Assert.NotNull(thrown);

        for (var candidate = thrown; candidate is not null; candidate = candidate.InnerException)
            if (candidate is SqlException sqlException)
            {
                Assert.Equal(expectedNumber, sqlException.Number);
                return;
            }

        Assert.Fail($"Expected a SqlException (Number={expectedNumber}) somewhere in the chain of {thrown}.");
    }
}

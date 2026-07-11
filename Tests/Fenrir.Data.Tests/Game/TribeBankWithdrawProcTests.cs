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
public class TribeBankWithdrawProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;
    private readonly ITribeRepository _tribes;

    public TribeBankWithdrawProcTests(SqlServerFixture fixture)
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
    public async Task WithdrawBankAsync_EmptiesTheSlot_AndCreditsTheCharactersMoney()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync();
        await SeedBankSlotAsync(tribeId, 4, 50_000);
        await SeedMoneyAsync(characterId, 10_000);

        var newMoney = await _tribes.WithdrawBankAsync(tribeId, 4, characterId, CancellationToken.None);

        Assert.Equal(60_000, newMoney);
        Assert.Equal(0, await ScalarAsync<int>(
            $"SELECT Amount FROM game.TribeBank WHERE TribeId = {tribeId} AND SlotIndex = 4;"));
        Assert.Equal(60_000L, await ScalarAsync<long>(
            $"SELECT Money FROM game.Characters WHERE CharacterId = {characterId};"));
    }

    [Fact]
    public async Task WithdrawBankAsync_EmptySlot_ThrowsAndChangesNothing()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync();
        await SeedMoneyAsync(characterId, 10_000);

        await AssertSqlErrorAsync(50210,
            () => _tribes.WithdrawBankAsync(tribeId, 7, characterId, CancellationToken.None).AsTask());

        Assert.Equal(10_000L, await ScalarAsync<long>(
            $"SELECT Money FROM game.Characters WHERE CharacterId = {characterId};"));
    }

    [Fact]
    public async Task WithdrawBankAsync_WouldExceedMoneyCap_ThrowsAndLeavesTheBankSlotUntouched()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync();
        await SeedBankSlotAsync(tribeId, 2, 50);
        await SeedMoneyAsync(characterId, 2_000_000_000);

        await AssertSqlErrorAsync(50261,
            () => _tribes.WithdrawBankAsync(tribeId, 2, characterId, CancellationToken.None).AsTask());

        Assert.Equal(50, await ScalarAsync<int>(
            $"SELECT Amount FROM game.TribeBank WHERE TribeId = {tribeId} AND SlotIndex = 2;"));
        Assert.Equal(2_000_000_000L, await ScalarAsync<long>(
            $"SELECT Money FROM game.Characters WHERE CharacterId = {characterId};"));
    }

    private async Task<byte> CreateTribeAsync()
    {
        const byte tribeId = 0;

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
        var accountId = await _accounts.CreateAsync($"tribebanktest-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"B{Guid.NewGuid():N}"[..8],
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

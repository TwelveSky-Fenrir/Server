using System.Data;
using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

// game.usp_TribeSubMaster_Set/Clear against real SQL Server 2025. game.Tribes has no seed script (only 4
// fixed rows normally created via usp_WorldState_EnsureInitialized) -- each test seeds its own TribeId row
// directly to stay independent of that bootstrap ordering.
[Collection("SqlServer")]
public class TribeSubMasterProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;

    public TribeSubMasterProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task TribeSubMaster_SetAndClear_CoverTheSlotLifecycle_AndGuardDuplicates()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync();
        var otherCharacterId = await CreateCharacterAsync();

        await ExecProcAsync("game.usp_TribeSubMaster_Set",
            ("TribeId", tribeId), ("SlotIndex", (byte)0), ("CharacterId", characterId));

        Assert.Equal(characterId, await ScalarAsync<int>(
            $"SELECT CharacterId FROM game.TribeSubMasters WHERE TribeId = {tribeId} AND SlotIndex = 0;"));

        // Same slot, different character -> 50310 (slot already occupied).
        var slotTaken = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_TribeSubMaster_Set",
            ("TribeId", tribeId), ("SlotIndex", (byte)0), ("CharacterId", otherCharacterId)));
        Assert.Equal(50310, slotTaken.Number);

        // Same character, a DIFFERENT slot -> 50311 (already holds a slot in this tribe).
        var alreadySubMaster = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync(
            "game.usp_TribeSubMaster_Set",
            ("TribeId", tribeId), ("SlotIndex", (byte)1), ("CharacterId", characterId)));
        Assert.Equal(50311, alreadySubMaster.Number);

        await ExecProcAsync("game.usp_TribeSubMaster_Clear", ("TribeId", tribeId), ("CharacterId", characterId));
        Assert.Equal(0, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.TribeSubMasters WHERE TribeId = {tribeId};"));

        var clearAgain = await Record.ExceptionAsync(() =>
            ExecProcAsync("game.usp_TribeSubMaster_Clear", ("TribeId", tribeId), ("CharacterId", characterId)));
        Assert.Null(clearAgain);

        // The freed slot can now be reused by a different character.
        await ExecProcAsync("game.usp_TribeSubMaster_Set",
            ("TribeId", tribeId), ("SlotIndex", (byte)0), ("CharacterId", otherCharacterId));
        Assert.Equal(otherCharacterId, await ScalarAsync<int>(
            $"SELECT CharacterId FROM game.TribeSubMasters WHERE TribeId = {tribeId} AND SlotIndex = 0;"));
    }

    private async Task<byte> CreateTribeAsync()
    {
        // TribeId is a fixed 0-3 domain value (CK_Tribes_TribeId); no throwaway ids are possible here.
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
        var accountId = await _accounts.CreateAsync($"tribetest-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"T{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
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

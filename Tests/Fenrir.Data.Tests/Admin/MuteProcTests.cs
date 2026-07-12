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

namespace Fenrir.Data.Tests.Admin;

[Collection("SqlServer")]
public class MuteProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;

    public MuteProcTests(SqlServerFixture fixture)
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
    public async Task Mute_GetActiveForCharacter_SeesCharacterLevelAndAccountLevelMutes_ButNotExpiredOnes()
    {
        var (accountId, characterId) = await CreateCharacterAsync();

        Assert.Equal(0, await CountActiveAsync(characterId));

        await CreateMuteAsync(null, characterId, 1, null);
        Assert.Equal(1, await CountActiveAsync(characterId));

        await CreateMuteAsync(accountId, null, 2, DateTime.UtcNow.AddHours(1));
        Assert.Equal(2, await CountActiveAsync(characterId));

        await CreateMuteAsync(null, characterId, 3, DateTime.UtcNow.AddHours(-1));
        Assert.Equal(2, await CountActiveAsync(characterId));
    }

    [Fact]
    public async Task Mute_Lift_DeactivatesWithoutDeleting_AndIsIdempotent()
    {
        var (_, characterId) = await CreateCharacterAsync();
        var muteId = await CreateMuteAsync(null, characterId, 1, null);

        Assert.Equal(1, await CountActiveAsync(characterId));

        await ExecProcAsync("admin.usp_Mute_Lift", ("MuteId", muteId));

        Assert.Equal(0, await CountActiveAsync(characterId));

        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM admin.Mutes WHERE MuteId = {muteId};"));

        var firstLift = await ScalarAsync<DateTime>($"SELECT LiftedAtUtc FROM admin.Mutes WHERE MuteId = {muteId};");
        var replay = await Record.ExceptionAsync(() => ExecProcAsync("admin.usp_Mute_Lift", ("MuteId", muteId)));
        Assert.Null(replay);
        Assert.Equal(firstLift,
            await ScalarAsync<DateTime>($"SELECT LiftedAtUtc FROM admin.Mutes WHERE MuteId = {muteId};"));

        var unknown = await Record.ExceptionAsync(() => ExecProcAsync("admin.usp_Mute_Lift", ("MuteId", -1)));
        Assert.Null(unknown);
    }

    [Fact]
    public async Task Mute_Create_RequiresATarget()
    {
        var targetless = await Assert.ThrowsAsync<SqlException>(() =>
            CreateMuteAsync(null, null, 1, null));
        Assert.Equal(50306, targetless.Number);
    }

    [Fact]
    public async Task Mute_Create_WithActorIds_PersistsThemIndependentlyOfTheTargetPair()
    {
        var (_, targetCharacterId) = await CreateCharacterAsync();
        var (gmAccountId, gmCharacterId) = await CreateCharacterAsync();

        var muteId = await CreateMuteAsync(null, targetCharacterId, 1, null, gmAccountId, gmCharacterId);

        Assert.Equal(gmAccountId,
            await ScalarAsync<int>($"SELECT ActorAccountId FROM admin.Mutes WHERE MuteId = {muteId};"));
        Assert.Equal(gmCharacterId,
            await ScalarAsync<int>($"SELECT ActorCharacterId FROM admin.Mutes WHERE MuteId = {muteId};"));
    }

    [Fact]
    public async Task Mute_Create_WithoutActorIds_PersistsNullActorColumns()
    {
        var (_, characterId) = await CreateCharacterAsync();

        var muteId = await CreateMuteAsync(null, characterId, 1, null);

        Assert.Equal(1, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM admin.Mutes WHERE MuteId = {muteId} AND ActorAccountId IS NULL AND ActorCharacterId IS NULL;"));
    }

    private async Task<(int AccountId, int CharacterId)> CreateCharacterAsync()
    {
        var accountId = await _accounts.CreateAsync($"mutetest-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        var characterId = await _characters.CreateAsync(
            accountId, 0, $"M{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);

        return (accountId, characterId);
    }

    private async Task<int> CreateMuteAsync(int? accountId, int? characterId, byte reason, DateTime? expiresAtUtc,
        int? actorAccountId = null, int? actorCharacterId = null)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("admin.usp_Mute_Create", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("AccountId", (object?)accountId ?? DBNull.Value);
        command.Parameters.AddWithValue("CharacterId", (object?)characterId ?? DBNull.Value);
        command.Parameters.AddWithValue("Reason", reason);
        command.Parameters.AddWithValue("ExpiresAtUtc", (object?)expiresAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("ActorAccountId", (object?)actorAccountId ?? DBNull.Value);
        command.Parameters.AddWithValue("ActorCharacterId", (object?)actorCharacterId ?? DBNull.Value);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task<int> CountActiveAsync(int characterId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("admin.usp_Mute_GetActiveForCharacter", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("CharacterId", characterId);

        var count = 0;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            count++;
        return count;
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

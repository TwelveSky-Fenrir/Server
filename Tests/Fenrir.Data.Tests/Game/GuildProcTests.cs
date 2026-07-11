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

[Collection("SqlServer")]
public class GuildProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;

    public GuildProcTests(SqlServerFixture fixture)
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
    public async Task Guild_Create_ReturnsTheGuildId_EnrollsTheMaster_AndGuardsNameAndMembership()
    {
        var masterId = await CreateCharacterAsync();
        var name = NewGuildName();

        var guildId = await CreateGuildAsync(name, masterId);
        Assert.True(guildId > 0);

        var role = await ScalarAsync<byte>(
            $"SELECT Role FROM game.GuildMembers WHERE GuildId = {guildId} AND CharacterId = {masterId};");
        Assert.Equal(2, role);

        var otherId = await CreateCharacterAsync();
        var nameTaken = await Assert.ThrowsAsync<SqlException>(() => CreateGuildAsync(name, otherId));
        Assert.Equal(50230, nameTaken.Number);

        var alreadyMember = await Assert.ThrowsAsync<SqlException>(() => CreateGuildAsync(NewGuildName(), masterId));
        Assert.Equal(50231, alreadyMember.Number);
    }

    [Fact]
    public async Task GuildMember_AddSetRoleSetMasterRemove_CoverTheMembershipLifecycle()
    {
        var masterId = await CreateCharacterAsync();
        var memberId = await CreateCharacterAsync();
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);

        var unknownGuild = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_GuildMember_Add",
            ("GuildId", -1), ("CharacterId", memberId), ("Role", (byte)0)));
        Assert.Equal(50235, unknownGuild.Number);

        await ExecProcAsync("game.usp_GuildMember_Add",
            ("GuildId", guildId), ("CharacterId", memberId), ("Role", (byte)0));

        var doubleAdd = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_GuildMember_Add",
            ("GuildId", guildId), ("CharacterId", memberId), ("Role", (byte)0)));
        Assert.Equal(50231, doubleAdd.Number);

        await ExecProcAsync("game.usp_GuildMember_SetRole",
            ("GuildId", guildId), ("CharacterId", memberId), ("Role", (byte)1));
        Assert.Equal(1, await ScalarAsync<byte>(
            $"SELECT Role FROM game.GuildMembers WHERE GuildId = {guildId} AND CharacterId = {memberId};"));

        var notAMember = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_GuildMember_SetRole",
            ("GuildId", guildId), ("CharacterId", -1), ("Role", (byte)1)));
        Assert.Equal(50233, notAMember.Number);

        await ExecProcAsync("game.usp_Guild_SetMaster",
            ("GuildId", guildId), ("NewMasterCharacterId", memberId));
        Assert.Equal(memberId, await ScalarAsync<int>(
            $"SELECT MasterCharacterId FROM game.Guilds WHERE GuildId = {guildId};"));
        Assert.Equal(2, await ScalarAsync<byte>(
            $"SELECT Role FROM game.GuildMembers WHERE GuildId = {guildId} AND CharacterId = {memberId};"));
        Assert.Equal(0, await ScalarAsync<byte>(
            $"SELECT Role FROM game.GuildMembers WHERE GuildId = {guildId} AND CharacterId = {masterId};"));

        var strangerId = await CreateCharacterAsync();
        var notMember = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_Guild_SetMaster",
            ("GuildId", guildId), ("NewMasterCharacterId", strangerId)));
        Assert.Equal(50233, notMember.Number);

        await ExecProcAsync("game.usp_GuildMember_Remove", ("GuildId", guildId), ("CharacterId", masterId));
        var removeAgain = await Record.ExceptionAsync(() =>
            ExecProcAsync("game.usp_GuildMember_Remove", ("GuildId", guildId), ("CharacterId", masterId)));
        Assert.Null(removeAgain);
    }

    [Fact]
    public async Task Guild_SetBuffSetLogoSetGrade_UpdateTheRow_AndThrowForAnUnknownGuild()
    {
        var masterId = await CreateCharacterAsync();
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);

        await ExecProcAsync("game.usp_Guild_SetBuff",
            ("GuildId", guildId), ("BuffType", 2), ("BuffState", 1), ("BuffTime", 3600), ("BuffTimeForDiff", 123L));
        await ExecProcAsync("game.usp_Guild_SetLogo", ("GuildId", guildId), ("Logo", 42));
        await ExecProcAsync("game.usp_Guild_SetGrade", ("GuildId", guildId), ("Grade", 3));

        Assert.Equal(2, await ScalarAsync<int>($"SELECT BuffType FROM game.Guilds WHERE GuildId = {guildId};"));
        Assert.Equal(123L, await ScalarAsync<long>(
            $"SELECT BuffTimeForDiff FROM game.Guilds WHERE GuildId = {guildId};"));
        Assert.Equal(42, await ScalarAsync<int>($"SELECT Logo FROM game.Guilds WHERE GuildId = {guildId};"));
        Assert.Equal(3, await ScalarAsync<int>($"SELECT Grade FROM game.Guilds WHERE GuildId = {guildId};"));

        var unknown = await Assert.ThrowsAsync<SqlException>(() =>
            ExecProcAsync("game.usp_Guild_SetLogo", ("GuildId", -1), ("Logo", 1)));
        Assert.Equal(50235, unknown.Number);
    }

    [Fact]
    public async Task Guild_AdjustPoints_AddsAndSpends_ButRejectsGoingNegative()
    {
        var masterId = await CreateCharacterAsync();
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);

        await ExecProcAsync("game.usp_Guild_AdjustPoints", ("GuildId", guildId), ("Delta", 10));
        await ExecProcAsync("game.usp_Guild_AdjustPoints", ("GuildId", guildId), ("Delta", -4));
        Assert.Equal(6, await ScalarAsync<int>($"SELECT Points FROM game.Guilds WHERE GuildId = {guildId};"));

        var overdraft = await Assert.ThrowsAsync<SqlException>(() =>
            ExecProcAsync("game.usp_Guild_AdjustPoints", ("GuildId", guildId), ("Delta", -7)));
        Assert.Equal(50234, overdraft.Number);
        Assert.Equal(6, await ScalarAsync<int>($"SELECT Points FROM game.Guilds WHERE GuildId = {guildId};"));
    }

    [Fact]
    public async Task Guild_GetTopByPoints_OrdersDescending_AndRespectsCount()
    {
        var lowId = await CreateGuildAsync(NewGuildName(), await CreateCharacterAsync());
        var highId = await CreateGuildAsync(NewGuildName(), await CreateCharacterAsync());
        var midId = await CreateGuildAsync(NewGuildName(), await CreateCharacterAsync());
        await ExecProcAsync("game.usp_Guild_AdjustPoints", ("GuildId", lowId), ("Delta", 10));
        await ExecProcAsync("game.usp_Guild_AdjustPoints", ("GuildId", highId), ("Delta", 900));
        await ExecProcAsync("game.usp_Guild_AdjustPoints", ("GuildId", midId), ("Delta", 500));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("game.usp_Guild_GetTopByPoints", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("Count", 2);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(highId, reader.GetInt32(reader.GetOrdinal("GuildId")));
        Assert.Equal(900, reader.GetInt32(reader.GetOrdinal("Points")));

        Assert.True(await reader.ReadAsync());
        Assert.Equal(midId, reader.GetInt32(reader.GetOrdinal("GuildId")));
        Assert.Equal(500, reader.GetInt32(reader.GetOrdinal("Points")));

        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task Guild_GetAll_IncludesEveryGuild_WithItsBuffFields()
    {
        var masterId = await CreateCharacterAsync();
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);
        await ExecProcAsync("game.usp_Guild_SetBuff",
            ("GuildId", guildId), ("BuffType", 3), ("BuffState", 1), ("BuffTime", 42), ("BuffTimeForDiff", 7L));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("game.usp_Guild_GetAll", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        await using var reader = await command.ExecuteReaderAsync();

        var found = false;
        while (await reader.ReadAsync())
        {
            if (reader.GetInt32(reader.GetOrdinal("GuildId")) != guildId)
                continue;

            found = true;
            Assert.Equal(3, reader.GetInt32(reader.GetOrdinal("BuffType")));
            Assert.Equal(1, reader.GetInt32(reader.GetOrdinal("BuffState")));
            Assert.Equal(42, reader.GetInt32(reader.GetOrdinal("BuffTime")));
            Assert.Equal(7L, reader.GetInt64(reader.GetOrdinal("BuffTimeForDiff")));
        }

        Assert.True(found, "usp_Guild_GetAll must include every guild, this one included.");
    }

    [Fact]
    public async Task Guild_Disband_RemovesGuildMembersAndNotices_AndThrowsForAnUnknownGuild()
    {
        var masterId = await CreateCharacterAsync();
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);

        await ExecProcAsync("game.usp_GuildNotice_Set",
            ("GuildId", guildId), ("NoticeIndex", (byte)0), ("Text", "farewell"));

        await ExecProcAsync("game.usp_Guild_Disband", ("GuildId", guildId), ("CharacterId", masterId));

        Assert.Equal(0, await ScalarAsync<int>($"SELECT COUNT(*) FROM game.Guilds WHERE GuildId = {guildId};"));
        Assert.Equal(0, await ScalarAsync<int>($"SELECT COUNT(*) FROM game.GuildMembers WHERE GuildId = {guildId};"));
        Assert.Equal(0, await ScalarAsync<int>($"SELECT COUNT(*) FROM game.GuildNotices WHERE GuildId = {guildId};"));

        var again = await Assert.ThrowsAsync<SqlException>(() =>
            ExecProcAsync("game.usp_Guild_Disband", ("GuildId", guildId), ("CharacterId", masterId)));
        Assert.Equal(50235, again.Number);
    }

    [Fact]
    public async Task Guild_CreateAndDebitMoney_WritesAGuildMoneyEventLogRow_WithTheDebitedAmount()
    {
        var masterId = await CreateCharacterWithMoneyAsync(10_000_000);
        var accountId =
            await ScalarAsync<int>($"SELECT AccountId FROM game.Characters WHERE CharacterId = {masterId};");
        var name = NewGuildName();

        var guildId = await CreateAndDebitMoneyAsync(name, masterId, -10_000_000, 0);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT TOP 1 EventCode, Category, ActorAccountId, ActorCharacterId, DeltaMoney, Outcome, Payload " +
            "FROM game.EventLog WHERE ActorCharacterId = @CharacterId ORDER BY EventLogId DESC;", connection);
        command.Parameters.AddWithValue("CharacterId", masterId);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt16(reader.GetOrdinal("EventCode")));
        Assert.Equal(11, reader.GetByte(reader.GetOrdinal("Category")));
        Assert.Equal(accountId, reader.GetInt32(reader.GetOrdinal("ActorAccountId")));
        Assert.Equal(masterId, reader.GetInt32(reader.GetOrdinal("ActorCharacterId")));
        Assert.Equal(-10_000_000L, reader.GetInt64(reader.GetOrdinal("DeltaMoney")));
        Assert.Equal(1, reader.GetByte(reader.GetOrdinal("Outcome")));
        var payload = reader.GetString(reader.GetOrdinal("Payload"));
        Assert.Contains($"GuildId={guildId}", payload);
        Assert.Contains("Grade=1", payload);
    }

    [Fact]
    public async Task Guild_UpgradeAndDebitMoney_WritesAGuildMoneyEventLogRow_WithTheResultingGrade()
    {
        var masterId = await CreateCharacterWithMoneyAsync(20_000_000);
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);

        await ExecProcAsync("game.usp_Guild_UpgradeAndDebitMoney",
            ("GuildId", guildId), ("Grade", 2), ("CharacterId", masterId), ("DeltaMoney", -20_000_000L),
            ("DeltaBigMoney", 0));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT TOP 1 EventCode, Category, ActorCharacterId, DeltaMoney, Outcome, Payload " +
            "FROM game.EventLog WHERE ActorCharacterId = @CharacterId ORDER BY EventLogId DESC;", connection);
        command.Parameters.AddWithValue("CharacterId", masterId);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt16(reader.GetOrdinal("EventCode")));
        Assert.Equal(11, reader.GetByte(reader.GetOrdinal("Category")));
        Assert.Equal(-20_000_000L, reader.GetInt64(reader.GetOrdinal("DeltaMoney")));
        Assert.Equal(1, reader.GetByte(reader.GetOrdinal("Outcome")));
        Assert.Contains("Grade=2", reader.GetString(reader.GetOrdinal("Payload")));
    }

    [Fact]
    public async Task Guild_Disband_WritesAZeroDeltaGuildMoneyEventLogRow()
    {
        var masterId = await CreateCharacterAsync();
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);
        await ExecProcAsync("game.usp_Guild_SetGrade", ("GuildId", guildId), ("Grade", 3));

        await ExecProcAsync("game.usp_Guild_Disband", ("GuildId", guildId), ("CharacterId", masterId));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT TOP 1 EventCode, Category, ActorCharacterId, DeltaMoney, Outcome, Payload " +
            "FROM game.EventLog WHERE ActorCharacterId = @CharacterId ORDER BY EventLogId DESC;", connection);
        command.Parameters.AddWithValue("CharacterId", masterId);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(3, reader.GetInt16(reader.GetOrdinal("EventCode")));
        Assert.Equal(11, reader.GetByte(reader.GetOrdinal("Category")));
        Assert.Equal(0L, reader.GetInt64(reader.GetOrdinal("DeltaMoney")));
        Assert.Equal(1, reader.GetByte(reader.GetOrdinal("Outcome")));
        var payload = reader.GetString(reader.GetOrdinal("Payload"));
        Assert.Contains($"GuildId={guildId}", payload);
        Assert.Contains("Grade=3", payload);
    }

    [Fact]
    public async Task Guild_CreateAndDebitMoney_InsufficientBalance_WritesNoEventLogRow()
    {
        var masterId = await CreateCharacterAsync();

        await Assert.ThrowsAsync<SqlException>(() => CreateAndDebitMoneyAsync(NewGuildName(), masterId,
            -10_000_000, 0));

        var count = await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.EventLog WHERE ActorCharacterId = {masterId} AND Category = 11;");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GuildMember_SetCallName_UpdatesTheRow_AndThrowsForANonMember()
    {
        var masterId = await CreateCharacterAsync();
        var memberId = await CreateCharacterAsync();
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);
        await ExecProcAsync("game.usp_GuildMember_Add", ("GuildId", guildId), ("CharacterId", memberId),
            ("Role", (byte)0));

        await ExecProcAsync("game.usp_GuildMember_SetCallName",
            ("GuildId", guildId), ("CharacterId", memberId), ("CallName", "Duke"));
        Assert.Equal("Duke", await ScalarAsync<string>(
            $"SELECT CallName FROM game.GuildMembers WHERE GuildId = {guildId} AND CharacterId = {memberId};"));

        await ExecProcAsync("game.usp_GuildMember_SetCallName",
            ("GuildId", guildId), ("CharacterId", memberId), ("CallName", ""));
        Assert.Equal("", await ScalarAsync<string>(
            $"SELECT CallName FROM game.GuildMembers WHERE GuildId = {guildId} AND CharacterId = {memberId};"));

        var notAMember = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync(
            "game.usp_GuildMember_SetCallName", ("GuildId", guildId), ("CharacterId", -1), ("CallName", "X")));
        Assert.Equal(50233, notAMember.Number);
    }

    [Fact]
    public async Task Guild_GetById_ReturnsTheRow_WithMemberCount_AndNoRowForAnUnknownGuild()
    {
        var masterId = await CreateCharacterAsync();
        var memberId = await CreateCharacterAsync();
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);
        await ExecProcAsync("game.usp_GuildMember_Add", ("GuildId", guildId), ("CharacterId", memberId),
            ("Role", (byte)0));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("game.usp_Guild_GetById", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("GuildId", guildId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(guildId, reader.GetInt32(reader.GetOrdinal("GuildId")));
        Assert.Equal(1, reader.GetInt32(reader.GetOrdinal("Grade")));
        Assert.Equal(2, reader.GetInt32(reader.GetOrdinal("MemberCount")));
        Assert.False(await reader.ReadAsync());
        await reader.CloseAsync();

        command.Parameters["GuildId"].Value = -1;
        await using var emptyReader = await command.ExecuteReaderAsync();
        Assert.False(await emptyReader.ReadAsync());
    }

    private async Task<int> CreateCharacterAsync()
    {
        var accountId = await _accounts.CreateAsync($"gldtest-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"G{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
    }

        private async Task<int> CreateCharacterWithMoneyAsync(long money)
    {
        var characterId = await CreateCharacterAsync();
        await _characters.AdjustMoneyAsync(characterId, money, 0, CancellationToken.None);
        return characterId;
    }

    private async Task<int> CreateAndDebitMoneyAsync(string name, int masterCharacterId, long deltaMoney,
        int deltaBigMoney)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("game.usp_Guild_CreateAndDebitMoney", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("Name", name);
        command.Parameters.AddWithValue("MasterCharacterId", masterCharacterId);
        command.Parameters.AddWithValue("DeltaMoney", deltaMoney);
        command.Parameters.AddWithValue("DeltaBigMoney", deltaBigMoney);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task<int> CreateGuildAsync(string name, int masterCharacterId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("game.usp_Guild_Create", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("Name", name);
        command.Parameters.AddWithValue("MasterCharacterId", masterCharacterId);
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

    private static string NewGuildName()
    {
        return $"G{Guid.NewGuid():N}"[..10];
    }
}

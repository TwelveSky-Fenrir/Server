using System.Data;
using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

// game.usp_Guild_* / usp_GuildMember_* write procs against real SQL Server 2025, exercised via raw ADO.NET
// since the C# repository isn't wired up yet -- every documented THROW still needs coverage.
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

        // The master is enrolled with Role 2 in the same transaction.
        var role = await ScalarAsync<byte>(
            $"SELECT Role FROM game.GuildMembers WHERE GuildId = {guildId} AND CharacterId = {masterId};");
        Assert.Equal(2, role);

        // Same name again -> 50230, even with a different master.
        var otherId = await CreateCharacterAsync();
        var nameTaken = await Assert.ThrowsAsync<SqlException>(() => CreateGuildAsync(name, otherId));
        Assert.Equal(50230, nameTaken.Number);

        // A character already in a guild cannot found another one -> 50231.
        var alreadyMember = await Assert.ThrowsAsync<SqlException>(() => CreateGuildAsync(NewGuildName(), masterId));
        Assert.Equal(50231, alreadyMember.Number);
    }

    [Fact]
    public async Task GuildMember_AddSetRoleSetMasterRemove_CoverTheMembershipLifecycle()
    {
        var masterId = await CreateCharacterAsync();
        var memberId = await CreateCharacterAsync();
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);

        // Add into an unknown guild -> 50235.
        var unknownGuild = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_GuildMember_Add",
            ("GuildId", -1), ("CharacterId", memberId), ("Role", (byte)0)));
        Assert.Equal(50235, unknownGuild.Number);

        await ExecProcAsync("game.usp_GuildMember_Add",
            ("GuildId", guildId), ("CharacterId", memberId), ("Role", (byte)0));

        // The same character cannot be added twice / to another guild -> 50231.
        var doubleAdd = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_GuildMember_Add",
            ("GuildId", guildId), ("CharacterId", memberId), ("Role", (byte)0)));
        Assert.Equal(50231, doubleAdd.Number);

        // Promote to sub-master; a role change landing on nobody -> 50233.
        await ExecProcAsync("game.usp_GuildMember_SetRole",
            ("GuildId", guildId), ("CharacterId", memberId), ("Role", (byte)1));
        Assert.Equal(1, await ScalarAsync<byte>(
            $"SELECT Role FROM game.GuildMembers WHERE GuildId = {guildId} AND CharacterId = {memberId};"));

        var notAMember = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_GuildMember_SetRole",
            ("GuildId", guildId), ("CharacterId", -1), ("Role", (byte)1)));
        Assert.Equal(50233, notAMember.Number);

        // Leadership transfer keeps all three leadership facts consistent.
        await ExecProcAsync("game.usp_Guild_SetMaster",
            ("GuildId", guildId), ("NewMasterCharacterId", memberId));
        Assert.Equal(memberId, await ScalarAsync<int>(
            $"SELECT MasterCharacterId FROM game.Guilds WHERE GuildId = {guildId};"));
        Assert.Equal(2, await ScalarAsync<byte>(
            $"SELECT Role FROM game.GuildMembers WHERE GuildId = {guildId} AND CharacterId = {memberId};"));
        Assert.Equal(0, await ScalarAsync<byte>(
            $"SELECT Role FROM game.GuildMembers WHERE GuildId = {guildId} AND CharacterId = {masterId};"));

        // Transferring to a non-member -> 50233.
        var strangerId = await CreateCharacterAsync();
        var notMember = await Assert.ThrowsAsync<SqlException>(() => ExecProcAsync("game.usp_Guild_SetMaster",
            ("GuildId", guildId), ("NewMasterCharacterId", strangerId)));
        Assert.Equal(50233, notMember.Number);

        // Remove is a silent, idempotent row deletion.
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
    public async Task Guild_Disband_RemovesGuildMembersAndNotices_AndThrowsForAnUnknownGuild()
    {
        var masterId = await CreateCharacterAsync();
        var guildId = await CreateGuildAsync(NewGuildName(), masterId);

        // Notice row proves the memory-optimized child table is swept too.
        await ExecProcAsync("game.usp_GuildNotice_Set",
            ("GuildId", guildId), ("NoticeIndex", (byte)0), ("Text", "farewell"));

        await ExecProcAsync("game.usp_Guild_Disband", ("GuildId", guildId));

        Assert.Equal(0, await ScalarAsync<int>($"SELECT COUNT(*) FROM game.Guilds WHERE GuildId = {guildId};"));
        Assert.Equal(0, await ScalarAsync<int>($"SELECT COUNT(*) FROM game.GuildMembers WHERE GuildId = {guildId};"));
        Assert.Equal(0, await ScalarAsync<int>($"SELECT COUNT(*) FROM game.GuildNotices WHERE GuildId = {guildId};"));

        var again = await Assert.ThrowsAsync<SqlException>(() =>
            ExecProcAsync("game.usp_Guild_Disband", ("GuildId", guildId)));
        Assert.Equal(50235, again.Number);
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

        // Clearing it back to "" is a valid, deliberate no-title state.
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
        Assert.Equal(1, reader.GetInt32(reader.GetOrdinal("Grade"))); // fresh guilds start at grade 1
        // MemberCount is INT (plain COUNT(*)), not BIGINT.
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

    // NVARCHAR(12), globally unique (UQ_Guilds_Name).
    private static string NewGuildName()
    {
        return $"G{Guid.NewGuid():N}"[..10];
    }
}

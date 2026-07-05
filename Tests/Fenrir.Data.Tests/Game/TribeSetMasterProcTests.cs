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

// game.usp_Tribe_SetMaster against real SQL Server 2025 -- TRIBE_WORK tSort 55's tally write, the first
// caller of this previously-uncalled proc (see ITribeRepository.SetMasterAsync).
[Collection("SqlServer")]
public class TribeSetMasterProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;
    private readonly ITribeRepository _tribes;

    public TribeSetMasterProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _tribes = new TribeRepository(db);
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task SetMasterAsync_AppointsAMemberOfTheTribe_AndIsReadBackByGetAll()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync(tribeId);

        await _tribes.SetMasterAsync(tribeId, characterId, CancellationToken.None);

        var tribes = await _tribes.GetAllAsync(CancellationToken.None);
        var tribe = Assert.Single(tribes, t => t.TribeId == tribeId);
        Assert.Equal(characterId, tribe.MasterCharacterId);
    }

    [Fact]
    public async Task SetMasterAsync_WithNull_VacatesTheSeat()
    {
        var tribeId = await CreateTribeAsync();
        var characterId = await CreateCharacterAsync(tribeId);
        await _tribes.SetMasterAsync(tribeId, characterId, CancellationToken.None);

        await _tribes.SetMasterAsync(tribeId, null, CancellationToken.None);

        var tribes = await _tribes.GetAllAsync(CancellationToken.None);
        var tribe = Assert.Single(tribes, t => t.TribeId == tribeId);
        Assert.Null(tribe.MasterCharacterId);
    }

    [Fact]
    public async Task SetMasterAsync_UnknownTribe_Throws50330()
    {
        const byte unknownTribeId = 200; // outside the real 0-3 domain, guaranteed absent

        await AssertSqlErrorAsync(50330,
            () => _tribes.SetMasterAsync(unknownTribeId, null, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task SetMasterAsync_CharacterNotInThisTribe_Throws50331()
    {
        var tribeId = await CreateTribeAsync();
        var otherTribeId = await CreateOtherTribeAsync(tribeId);
        var outsiderCharacterId = await CreateCharacterAsync(otherTribeId);

        await AssertSqlErrorAsync(50331,
            () => _tribes.SetMasterAsync(tribeId, outsiderCharacterId, CancellationToken.None).AsTask());
    }

    private async Task<byte> CreateTribeAsync()
    {
        // TribeId is a fixed 0-3 domain value (CK_Tribes_TribeId); no throwaway ids are possible here.
        const byte tribeId = 2;
        await EnsureTribeExistsAsync(tribeId);
        return tribeId;
    }

    private async Task<byte> CreateOtherTribeAsync(byte excluding)
    {
        var otherTribeId = (byte)((excluding + 1) % 4);
        await EnsureTribeExistsAsync(otherTribeId);
        return otherTribeId;
    }

    private async Task EnsureTribeExistsAsync(byte tribeId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "IF NOT EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = @TribeId) " +
            "INSERT INTO game.Tribes (TribeId) VALUES (@TribeId);", connection);
        command.Parameters.AddWithValue("TribeId", tribeId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> CreateCharacterAsync(byte tribeId)
    {
        var accountId = await _accounts.CreateAsync($"tribesetmastertest-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"M{Guid.NewGuid():N}"[..8],
            tribeId, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
    }

    // CaeriusNet wraps the driver's SqlException in its own CaeriusNetSqlException; unwrap by walking
    // InnerException rather than depending on that wrapper type directly.
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

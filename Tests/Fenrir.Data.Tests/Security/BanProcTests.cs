using System.Data;
using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Security;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Security;

// admin.usp_Ban_* against real SQL Server 2025, driven through BanRepository -- unlike mutes (checked once at
// world entry for the account AND the character in a single query), account-level and character-level bans are
// deliberately two separate checks at two separate choke points (Login / world entry): see BanRepository's own
// remarks and LoginHandler/EnterWorldHandler for why usp_Ban_GetActiveForCharacter never expands to the owning
// account (a character can only ever reach world entry after that account's login already passed the account check).
[Collection("SqlServer")]
public class BanProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly IBanRepository _bans;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;

    public BanProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _bans = new BanRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task IsActiveForAccountAsync_SeesAnAccountLevelBan_ButNotAnExpiredOrCharacterOnlyOne()
    {
        var (accountId, characterId) = await CreateCharacterAsync();

        Assert.False(await _bans.IsActiveForAccountAsync(accountId, CancellationToken.None));

        await CreateBanAsync(null, characterId, 1, null); // character-only ban must not leak into the account check
        Assert.False(await _bans.IsActiveForAccountAsync(accountId, CancellationToken.None));

        await CreateBanAsync(accountId, null, 2, DateTime.UtcNow.AddHours(-1)); // expired
        Assert.False(await _bans.IsActiveForAccountAsync(accountId, CancellationToken.None));

        await CreateBanAsync(accountId, null, 3, null); // permanent, active
        Assert.True(await _bans.IsActiveForAccountAsync(accountId, CancellationToken.None));
    }

    [Fact]
    public async Task IsActiveForCharacterAsync_SeesACharacterLevelBan_ButNotAnExpiredOrAccountOnlyOne()
    {
        var (accountId, characterId) = await CreateCharacterAsync();

        Assert.False(await _bans.IsActiveForCharacterAsync(characterId, CancellationToken.None));

        // Account-level ban deliberately doesn't show up here -- see the class remarks.
        await CreateBanAsync(accountId, null, 1, null);
        Assert.False(await _bans.IsActiveForCharacterAsync(characterId, CancellationToken.None));

        await CreateBanAsync(null, characterId, 2, DateTime.UtcNow.AddHours(-1)); // expired
        Assert.False(await _bans.IsActiveForCharacterAsync(characterId, CancellationToken.None));

        await CreateBanAsync(null, characterId, 3, DateTime.UtcNow.AddHours(1)); // active, time-boxed
        Assert.True(await _bans.IsActiveForCharacterAsync(characterId, CancellationToken.None));
    }

    [Fact]
    public async Task Ban_Create_RequiresATarget()
    {
        var targetless = await Assert.ThrowsAsync<SqlException>(() => CreateBanAsync(null, null, 1, null));
        Assert.Equal(50301, targetless.Number);
    }

    private async Task<(int AccountId, int CharacterId)> CreateCharacterAsync()
    {
        var accountId = await _accounts.CreateAsync($"bantest-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        var characterId = await _characters.CreateAsync(
            accountId, 0, $"B{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);

        return (accountId, characterId);
    }

    private async Task CreateBanAsync(int? accountId, int? characterId, byte reason, DateTime? expiresAtUtc)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("admin.usp_Ban_Create", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("AccountId", (object?)accountId ?? DBNull.Value);
        command.Parameters.AddWithValue("CharacterId", (object?)characterId ?? DBNull.Value);
        command.Parameters.AddWithValue("Reason", reason);
        command.Parameters.AddWithValue("ExpiresAtUtc", (object?)expiresAtUtc ?? DBNull.Value);
        await command.ExecuteScalarAsync();
    }
}

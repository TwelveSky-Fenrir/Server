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

    // GM-BLOCK (item A): IBanRepository.CreateAsync is the new create path -- covers the BanId it returns and
    // the GmManualBlock reason mapping legacy's own 603 doesn't fit into admin.Bans.Reason's TINYINT column.
    [Fact]
    public async Task CreateAsync_ReturnsANewBanId_AndTheBanIsThenVisibleAsActive()
    {
        var (accountId, characterId) = await CreateCharacterAsync();

        var banId = await _bans.CreateAsync(accountId, characterId, BanReason.GmManualBlock,
            DateTime.UtcNow.AddDays(365 * 30), CancellationToken.None);

        Assert.True(banId > 0);
        Assert.True(await _bans.IsActiveForAccountAsync(accountId, CancellationToken.None));
        Assert.True(await _bans.IsActiveForCharacterAsync(characterId, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_CalledTwiceForTheSameTarget_InsertsTwoIndependentRows()
    {
        // Not idempotent: a ban log, not a single-row-per-target flag (usp_Ban_Create's own remarks).
        var (accountId, characterId) = await CreateCharacterAsync();

        var first = await _bans.CreateAsync(accountId, characterId, BanReason.GmManualBlock, null,
            CancellationToken.None);
        var second = await _bans.CreateAsync(accountId, characterId, BanReason.GmManualBlock, null,
            CancellationToken.None);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task CreateAsync_NeitherAccountNorCharacterGiven_Throws50301()
    {
        var ex = await Assert.ThrowsAsync<SqlException>(() =>
            _bans.CreateAsync(null, null, BanReason.GmManualBlock, null, CancellationToken.None).AsTask());

        Assert.Equal(50301, ex.Number);
    }

    // gm-action-audit-attribution (Migrations/035_bans_actor_attribution.sql): admin.Bans now records which GM
    // issued the ban, independently of the existing target AccountId/CharacterId pair. Verified via a raw SELECT
    // since neither usp_Ban_GetActiveForAccount/Character nor BanRowDto project the new columns today (both only
    // ever consumed for their row COUNT).
    [Fact]
    public async Task CreateAsync_WithActorIds_PersistsThemIndependentlyOfTheTargetPair()
    {
        var (targetAccountId, targetCharacterId) = await CreateCharacterAsync();
        var (gmAccountId, gmCharacterId) = await CreateCharacterAsync();

        var banId = await _bans.CreateAsync(targetAccountId, targetCharacterId, BanReason.GmManualBlock,
            null, CancellationToken.None, gmAccountId, gmCharacterId);

        Assert.Equal(gmAccountId,
            await ScalarAsync<int>($"SELECT ActorAccountId FROM admin.Bans WHERE BanId = {banId};"));
        Assert.Equal(gmCharacterId,
            await ScalarAsync<int>($"SELECT ActorCharacterId FROM admin.Bans WHERE BanId = {banId};"));
    }

    // Actor ids are optional -- omitting them (every pre-existing call site) must persist NULL, not fail, and
    // must not disturb the existing target-pair behavior.
    [Fact]
    public async Task CreateAsync_WithoutActorIds_PersistsNullActorColumns()
    {
        var (accountId, characterId) = await CreateCharacterAsync();

        var banId = await _bans.CreateAsync(accountId, characterId, BanReason.GmManualBlock, null,
            CancellationToken.None);

        Assert.Equal(1, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM admin.Bans WHERE BanId = {banId} AND ActorAccountId IS NULL AND ActorCharacterId IS NULL;"));
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
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

    // Routed through the repository (rather than a raw SqlCommand) now that IBanRepository.CreateAsync exists --
    // exercises the exact same call path GmBlockAvatarService uses. reason is a plain byte here (not BanReason)
    // since these pre-existing tests need arbitrary distinct values to prove independent ban rows, not a real
    // domain reason.
    private async Task CreateBanAsync(int? accountId, int? characterId, byte reason, DateTime? expiresAtUtc)
    {
        await _bans.CreateAsync(accountId, characterId, (BanReason)reason, expiresAtUtc, CancellationToken.None);
    }
}

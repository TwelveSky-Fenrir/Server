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

        await CreateBanAsync(null, characterId, 1, null);
        Assert.False(await _bans.IsActiveForAccountAsync(accountId, CancellationToken.None));

        await CreateBanAsync(accountId, null, 2, DateTime.UtcNow.AddHours(-1));
        Assert.False(await _bans.IsActiveForAccountAsync(accountId, CancellationToken.None));

        await CreateBanAsync(accountId, null, 3, null);
        Assert.True(await _bans.IsActiveForAccountAsync(accountId, CancellationToken.None));
    }

    [Fact]
    public async Task IsActiveForCharacterAsync_SeesACharacterLevelBan_ButNotAnExpiredOrAccountOnlyOne()
    {
        var (accountId, characterId) = await CreateCharacterAsync();

        Assert.False(await _bans.IsActiveForCharacterAsync(characterId, CancellationToken.None));

        await CreateBanAsync(accountId, null, 1, null);
        Assert.False(await _bans.IsActiveForCharacterAsync(characterId, CancellationToken.None));

        await CreateBanAsync(null, characterId, 2, DateTime.UtcNow.AddHours(-1));
        Assert.False(await _bans.IsActiveForCharacterAsync(characterId, CancellationToken.None));

        await CreateBanAsync(null, characterId, 3, DateTime.UtcNow.AddHours(1));
        Assert.True(await _bans.IsActiveForCharacterAsync(characterId, CancellationToken.None));
    }

    [Fact]
    public async Task Ban_Create_RequiresATarget()
    {
        var targetless = await Record.ExceptionAsync(() => CreateBanAsync(null, null, 1, null));
        Assert.Equal(50301, Assert.IsType<SqlException>(targetless!.InnerException).Number);
    }

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
        var ex = await Record.ExceptionAsync(() =>
            _bans.CreateAsync(null, null, BanReason.GmManualBlock, null, CancellationToken.None).AsTask());

        Assert.Equal(50301, Assert.IsType<SqlException>(ex!.InnerException).Number);
    }

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

    private async Task CreateBanAsync(int? accountId, int? characterId, byte reason, DateTime? expiresAtUtc)
    {
        await _bans.CreateAsync(accountId, characterId, (BanReason)reason, expiresAtUtc, CancellationToken.None);
    }
}

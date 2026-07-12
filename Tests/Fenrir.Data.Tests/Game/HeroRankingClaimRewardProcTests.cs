using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Progression;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Progression;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

[Collection("SqlServer")]
public sealed class HeroRankingClaimRewardProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;
    private readonly IHeroRankingRepository _heroRankings;

    public HeroRankingClaimRewardProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _heroRankings = new HeroRankingRepository(db, provider.GetRequiredService<ICaeriusNetCache>());
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task ClaimRewardAsync_ClaimableRow_SetsRewardClaimedAndDurablyGrantsContributionPoints()
    {
        var tribeId = await EnsureTribeAsync();
        var characterId = await CreateCharacterAsync();
        await SeedUnclaimedPreviousPeriodRowAsync(characterId, tribeId, 500, 10);

        await _heroRankings.ClaimRewardAsync(characterId, 1, 300, CancellationToken.None);

        var previous = await _heroRankings.GetByPeriodAsync(1, CancellationToken.None);
        var row = previous.Single(r => r.CharacterId == characterId);
        Assert.True(row.RewardClaimed == true);

        var bundle = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(bundle);
        Assert.Equal(300, bundle.Character.ContributionPoints);
    }

    [Fact]
    public async Task ClaimRewardAsync_AlreadyClaimed_ThrowsAndDoesNotDoubleGrantContributionPoints()
    {
        var tribeId = await EnsureTribeAsync();
        var characterId = await CreateCharacterAsync();
        await SeedUnclaimedPreviousPeriodRowAsync(characterId, tribeId, 500, 10);

        await _heroRankings.ClaimRewardAsync(characterId, 1, 300, CancellationToken.None);

        var ex = await Record.ExceptionAsync(() =>
            _heroRankings.ClaimRewardAsync(characterId, 1, 300, CancellationToken.None).AsTask());

        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50357, sqlException.Number);

        var bundle = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(bundle);
        Assert.Equal(300, bundle.Character.ContributionPoints);
    }

    private async Task SeedUnclaimedPreviousPeriodRowAsync(int characterId, byte tribeId, int points, int level)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO game.HeroRankings (CharacterId, PeriodKind, Points, TribeId, Level, RewardClaimed) " +
            "VALUES (@CharacterId, 1, @Points, @TribeId, @Level, 0);", connection);
        command.Parameters.AddWithValue("CharacterId", characterId);
        command.Parameters.AddWithValue("Points", points);
        command.Parameters.AddWithValue("TribeId", tribeId);
        command.Parameters.AddWithValue("Level", level);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<byte> EnsureTribeAsync()
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
        var accountId = await _accounts.CreateAsync($"heroclaim-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"C{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
    }
}

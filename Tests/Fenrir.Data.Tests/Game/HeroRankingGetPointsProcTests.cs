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
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

[Collection("SqlServer")]
public sealed class HeroRankingGetPointsProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly IHeroRankingRepository _heroRankings;

    public HeroRankingGetPointsProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _heroRankings = new HeroRankingRepository(db);
    }

    [Fact]
    public async Task GetPointsAsync_NoRowForThisCharacterAndPeriod_ReturnsNull()
    {
        var characterId = await CreateCharacterAsync();

        var points = await _heroRankings.GetPointsAsync(characterId, 0, CancellationToken.None);

        Assert.Null(points);
    }

    [Fact]
    public async Task GetPointsAsync_RowExists_ReturnsTheStoredTotal()
    {
        var characterId = await CreateCharacterAsync();
        await _heroRankings.AddPointsAsync(characterId, 0, 250, 1, 42, CancellationToken.None);

        var points = await _heroRankings.GetPointsAsync(characterId, 0, CancellationToken.None);

        Assert.Equal(250, points);
    }

    [Fact]
    public async Task GetPointsAsync_ScopedToTheRequestedPeriod_DoesNotLeakTheOtherPeriodsTotal()
    {
        var characterId = await CreateCharacterAsync();
        await _heroRankings.MarkRewardClaimedAsync(characterId, 1, 999, 0, 10, CancellationToken.None);
        await _heroRankings.AddPointsAsync(characterId, 0, 4, 0, 10, CancellationToken.None);

        var current = await _heroRankings.GetPointsAsync(characterId, 0, CancellationToken.None);
        var previous = await _heroRankings.GetPointsAsync(characterId, 1, CancellationToken.None);

        Assert.Equal(4, current);
        Assert.Equal(999, previous);
    }

    private async Task<int> CreateCharacterAsync()
    {
        var accountId = await _accounts.CreateAsync($"herogetpts-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"A{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
    }
}

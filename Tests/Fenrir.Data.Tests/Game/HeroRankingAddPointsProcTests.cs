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
public sealed class HeroRankingAddPointsProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly IHeroRankingRepository _heroRankings;

    public HeroRankingAddPointsProcTests(SqlServerFixture fixture)
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
    }

    [Fact]
    public async Task AddPointsAsync_NoExistingRow_SeedsItWithTheDeltaAsTheTotal()
    {
        var characterId = await CreateCharacterAsync();

        var total = await _heroRankings.AddPointsAsync(characterId, 0, 7, 1, 42,
            CancellationToken.None);

        Assert.Equal(7, total);

        var rows = await _heroRankings.GetByPeriodAsync(0, CancellationToken.None);
        var row = Assert.Single(rows, r => r.CharacterId == characterId);
        Assert.Equal(7, row.Points);
        Assert.Equal((byte)1, row.TribeId);
        Assert.Equal((short)42, row.Level);
    }

    [Fact]
    public async Task AddPointsAsync_ExistingRow_AccumulatesInsteadOfOverwriting()
    {
        var characterId = await CreateCharacterAsync();

        await _heroRankings.AddPointsAsync(characterId, 0, 5, 0, 10, CancellationToken.None);
        var total = await _heroRankings.AddPointsAsync(characterId, 0, 3, 0, 10, CancellationToken.None);

        Assert.Equal(8, total);

        var rows = await _heroRankings.GetByPeriodAsync(0, CancellationToken.None);
        var row = Assert.Single(rows, r => r.CharacterId == characterId);
        Assert.Equal(8, row.Points);
    }

    [Fact]
    public async Task AddPointsAsync_RepeatedCallsAcrossMultipleFlushes_SumCorrectly()
    {
        var characterId = await CreateCharacterAsync();

        for (var i = 0; i < 5; i++)
            await _heroRankings.AddPointsAsync(characterId, 0, 1, 0, 10, CancellationToken.None);

        var rows = await _heroRankings.GetByPeriodAsync(0, CancellationToken.None);
        var row = Assert.Single(rows, r => r.CharacterId == characterId);
        Assert.Equal(5, row.Points);
    }

    [Fact]
    public async Task AddPointsAsync_NullTribeAndLevel_PreservesPreviouslyStoredValues()
    {
        var characterId = await CreateCharacterAsync();

        await _heroRankings.AddPointsAsync(characterId, 0, 1, 2, 55, CancellationToken.None);
        await _heroRankings.AddPointsAsync(characterId, 0, 1, null, null, CancellationToken.None);

        var rows = await _heroRankings.GetByPeriodAsync(0, CancellationToken.None);
        var row = Assert.Single(rows, r => r.CharacterId == characterId);
        Assert.Equal((byte)2, row.TribeId);
        Assert.Equal((short)55, row.Level);
        Assert.Equal(2, row.Points);
    }

    [Fact]
    public async Task AddPointsAsync_DoesNotDisturbTheOtherPeriodsRowForTheSameCharacter()
    {
        var characterId = await CreateCharacterAsync();

        await _heroRankings.MarkRewardClaimedAsync(characterId, 1, 999, 0, 10,
            CancellationToken.None);
        await _heroRankings.AddPointsAsync(characterId, 0, 4, 0, 10,
            CancellationToken.None);

        var previous = await _heroRankings.GetByPeriodAsync(1, CancellationToken.None);
        var previousRow = Assert.Single(previous, r => r.CharacterId == characterId);
        Assert.Equal(999, previousRow.Points);

        var current = await _heroRankings.GetByPeriodAsync(0, CancellationToken.None);
        var currentRow = Assert.Single(current, r => r.CharacterId == characterId);
        Assert.Equal(4, currentRow.Points);
    }

    private async Task<int> CreateCharacterAsync()
    {
        var accountId = await _accounts.CreateAsync($"heroadd-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"A{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
    }
}

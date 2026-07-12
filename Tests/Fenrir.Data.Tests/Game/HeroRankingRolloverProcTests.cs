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
public sealed class HeroRankingRolloverProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;
    private readonly IHeroRankingRepository _heroRankings;

    public HeroRankingRolloverProcTests(SqlServerFixture fixture)
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
    public async Task RolloverIfDueAsync_SentinelFresh_NeverFlips()
    {
        var rolledOver = await _heroRankings.RolloverIfDueAsync(CancellationToken.None);

        Assert.False(rolledOver);
    }

    [Fact]
    public async Task RolloverIfDueAsync_SentinelOlderThanSevenDays_FlipsCurrentIntoPreviousCappedAtTop10PerTribe()
    {
        var tribeId = await EnsureTribeAsync();

        var ranked = new List<int>();
        for (var i = 0; i < 11; i++)
        {
            var characterId = await CreateCharacterAsync();
            ranked.Add(characterId);
            await _heroRankings.MarkRewardClaimedAsync(characterId, 0, 1000 - i, tribeId,
                10, CancellationToken.None);
        }

        var zeroPointsCharacterId = await CreateCharacterAsync();
        await _heroRankings.MarkRewardClaimedAsync(zeroPointsCharacterId, 0, 0, tribeId,
            1, CancellationToken.None);

        var noTribeCharacterId = await CreateCharacterAsync();
        await _heroRankings.MarkRewardClaimedAsync(noTribeCharacterId, 0, 500, null,
            1, CancellationToken.None);

        var staleCharacterId = await CreateCharacterAsync();
        await _heroRankings.MarkRewardClaimedAsync(staleCharacterId, 1, 42, tribeId, 1,
            CancellationToken.None);

        await _heroRankings.RolloverIfDueAsync(CancellationToken.None);
        await BackdateSentinelAsync(TimeSpan.FromDays(8));

        var rolledOver = await _heroRankings.RolloverIfDueAsync(CancellationToken.None);
        Assert.True(rolledOver);

        var previous = await _heroRankings.GetByPeriodAsync(1, CancellationToken.None);
        var promoted = previous.Where(r => ranked.Contains(r.CharacterId)).ToList();

        Assert.Equal(10, promoted.Count);
        Assert.DoesNotContain(promoted, r => r.CharacterId == ranked[^1]);
        Assert.All(promoted, r => Assert.False(r.RewardClaimed == true));
        Assert.DoesNotContain(previous, r => r.CharacterId == zeroPointsCharacterId);
        Assert.DoesNotContain(previous, r => r.CharacterId == noTribeCharacterId);
        Assert.DoesNotContain(previous, r => r.CharacterId == staleCharacterId);

        var current = await _heroRankings.GetByPeriodAsync(0, CancellationToken.None);
        Assert.DoesNotContain(current, r => ranked.Contains(r.CharacterId));
        Assert.DoesNotContain(current, r => r.CharacterId == zeroPointsCharacterId);
        Assert.DoesNotContain(current, r => r.CharacterId == noTribeCharacterId);

        var secondCall = await _heroRankings.RolloverIfDueAsync(CancellationToken.None);
        Assert.False(secondCall);
    }

    private async Task BackdateSentinelAsync(TimeSpan age)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "UPDATE game.HeroRankingRolloverState SET LastRolloverAtUtc = DATEADD(DAY, @Days, SYSUTCDATETIME()) WHERE Id = 1;",
            connection);
        command.Parameters.AddWithValue("Days", -age.Days);
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
        var accountId = await _accounts.CreateAsync($"herorank-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"H{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
    }
}

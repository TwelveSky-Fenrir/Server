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

// game.usp_HeroRanking_Rollover against real SQL Server 2025. game.HeroRankingRolloverState is a true
// singleton (like game.WorldState), shared by every test in the "SqlServer" collection for the whole
// assembly run -- every path that writes LastRolloverAtUtc (row creation, a successful flip) sets it to
// "now", so a bare RolloverIfDueAsync call is always a deterministic no-op regardless of what any other
// test already did to it; only backdating it ourselves, inside a single test method, can ever make a flip
// due. No other test file in this suite touches game.HeroRankings, so the top-10-per-tribe cap assertions
// below only ever see rows this test itself inserted.
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

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _heroRankings = new HeroRankingRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task RolloverIfDueAsync_SentinelFresh_NeverFlips()
    {
        // Whether this is the very first call in the whole suite (creates the row with LastRolloverAtUtc =
        // now) or a later one (every prior writer also always left it at "now"), the 7-day gate can never be
        // satisfied here.
        var rolledOver = await _heroRankings.RolloverIfDueAsync(CancellationToken.None);

        Assert.False(rolledOver);
    }

    [Fact]
    public async Task RolloverIfDueAsync_SentinelOlderThanSevenDays_FlipsCurrentIntoPreviousCappedAtTop10PerTribe()
    {
        var tribeId = await EnsureTribeAsync();

        // 11 ranked characters for the same tribe: the cap must keep exactly the top 10 by Points and drop
        // the 11th (lowest-points) one, exactly like the legacy's own "ORDER BY hPoint DESC LIMIT 10".
        var ranked = new List<int>();
        for (var i = 0; i < 11; i++)
        {
            var characterId = await CreateCharacterAsync();
            ranked.Add(characterId);
            await _heroRankings.MarkRewardClaimedAsync(characterId, periodKind: 0, points: 1000 - i, tribeId,
                level: 10, CancellationToken.None);
        }

        // Must never be promoted: zero points ("if (hPoint < 1) continue" in the legacy) ...
        var zeroPointsCharacterId = await CreateCharacterAsync();
        await _heroRankings.MarkRewardClaimedAsync(zeroPointsCharacterId, periodKind: 0, points: 0, tribeId,
            level: 1, CancellationToken.None);

        // ... and no live tribe (unreachable via HeroRankBuilder/HeroRewardResolver anyway).
        var noTribeCharacterId = await CreateCharacterAsync();
        await _heroRankings.MarkRewardClaimedAsync(noTribeCharacterId, periodKind: 0, points: 500, tribeId: null,
            level: 1, CancellationToken.None);

        // A leftover Previous-period row from the last cycle: the rollover must replace it wholesale, not merge.
        var staleCharacterId = await CreateCharacterAsync();
        await _heroRankings.MarkRewardClaimedAsync(staleCharacterId, periodKind: 1, points: 42, tribeId, level: 1,
            CancellationToken.None);

        // Ensure the sentinel row exists, then force it stale enough for the 7-day gate to trip.
        await _heroRankings.RolloverIfDueAsync(CancellationToken.None);
        await BackdateSentinelAsync(TimeSpan.FromDays(8));

        var rolledOver = await _heroRankings.RolloverIfDueAsync(CancellationToken.None);
        Assert.True(rolledOver);

        var previous = await _heroRankings.GetByPeriodAsync(1, CancellationToken.None);
        var promoted = previous.Where(r => ranked.Contains(r.CharacterId)).ToList();

        Assert.Equal(10, promoted.Count);
        Assert.DoesNotContain(promoted, r => r.CharacterId == ranked[^1]); // the 11th, lowest-points entrant
        Assert.All(promoted, r => Assert.False(r.RewardClaimed == true)); // fresh period, nothing claimed yet
        Assert.DoesNotContain(previous, r => r.CharacterId == zeroPointsCharacterId);
        Assert.DoesNotContain(previous, r => r.CharacterId == noTribeCharacterId);
        Assert.DoesNotContain(previous, r => r.CharacterId == staleCharacterId);

        var current = await _heroRankings.GetByPeriodAsync(0, CancellationToken.None);
        Assert.DoesNotContain(current, r => ranked.Contains(r.CharacterId));
        Assert.DoesNotContain(current, r => r.CharacterId == zeroPointsCharacterId);
        Assert.DoesNotContain(current, r => r.CharacterId == noTribeCharacterId);

        // Idempotent: immediately calling again must not roll a second time.
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
        // TribeId is a fixed 0-3 domain value (CK_Tribes_TribeId); idempotent so it doesn't matter whether
        // another test class already created it.
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

using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Guilds;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

// game.Guilds is a shared, cross-test table (usp_Guild_AdjustPoints has no other caller anywhere in the
// codebase, so only test code ever writes Points), so every assertion here is deliberately RELATIVE to
// guilds created within the same test rather than an assumed absolute RankNo -- a hardcoded "this guild is
// rank 1" would silently break the moment any other test in the suite creates a higher-scoring guild.
[Collection("SqlServer")]
public class GuildGetRankingProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;
    private readonly IGuildRepository _guilds;

    public GuildGetRankingProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _guilds = new GuildRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task GetRankingAsync_OrdersByPointsDescending_SharesRankOnATie_AndCarriesMemberCount()
    {
        var lowMaster = await CreateCharacterAsync();
        var lowId = await _guilds.CreateAsync(NewGuildName(), lowMaster, CancellationToken.None);
        await _guilds.AdjustPointsAsync(lowId, 10, CancellationToken.None);

        var highMaster = await CreateCharacterAsync();
        var highId = await _guilds.CreateAsync(NewGuildName(), highMaster, CancellationToken.None);
        await _guilds.AdjustPointsAsync(highId, 900, CancellationToken.None);
        var highMember = await CreateCharacterAsync();
        await _guilds.AddMemberAsync(highId, highMember, CancellationToken.None);

        var tiedMaster = await CreateCharacterAsync();
        var tiedId = await _guilds.CreateAsync(NewGuildName(), tiedMaster, CancellationToken.None);
        await _guilds.AdjustPointsAsync(tiedId, 900, CancellationToken.None);

        var ranking = await _guilds.GetRankingAsync(10_000, CancellationToken.None);

        var high = ranking.Single(r => r.GuildId == highId);
        var tied = ranking.Single(r => r.GuildId == tiedId);
        var low = ranking.Single(r => r.GuildId == lowId);

        // Two guilds with EQUAL Points always share the same RankNo -- what distinguishes RANK() from
        // ROW_NUMBER(); true no matter what else is in the shared table, since both guilds have identically
        // many strictly-higher-scoring rows above them.
        Assert.Equal(high.RankNo, tied.RankNo);
        // A guild with strictly fewer points always ranks strictly worse (a numerically larger RankNo).
        Assert.True(low.RankNo > high.RankNo);
        // RANK() (not DENSE_RANK()) leaves a gap sized by how many rows shared the rank above it -- verified
        // against a live COUNT oracle instead of a hardcoded gap, so this holds regardless of any other
        // guild's points anywhere else in the shared table.
        var guildsAbove10 = await CountGuildsWithPointsGreaterThanAsync(10);
        Assert.Equal(1 + guildsAbove10, low.RankNo);

        Assert.Equal(2, high.MemberCount);
        Assert.Equal(1, tied.MemberCount);
        Assert.Equal(1, low.MemberCount);
    }

    [Fact]
    public async Task GetRankingAsync_RespectsCount_ReturningOnlyTheTopN()
    {
        var firstMaster = await CreateCharacterAsync();
        var firstId = await _guilds.CreateAsync(NewGuildName(), firstMaster, CancellationToken.None);
        await _guilds.AdjustPointsAsync(firstId, 500_000, CancellationToken.None);

        var secondMaster = await CreateCharacterAsync();
        var secondId = await _guilds.CreateAsync(NewGuildName(), secondMaster, CancellationToken.None);
        await _guilds.AdjustPointsAsync(secondId, 400_000, CancellationToken.None);

        var thirdMaster = await CreateCharacterAsync();
        var thirdId = await _guilds.CreateAsync(NewGuildName(), thirdMaster, CancellationToken.None);
        await _guilds.AdjustPointsAsync(thirdId, 300_000, CancellationToken.None);

        // 500_000/400_000/300_000 sit comfortably above every other Points value this test suite ever
        // creates (max observed elsewhere is in the low thousands), so firstId/secondId are guaranteed to be
        // the two top-ranked rows across the whole shared table.
        var top2 = await _guilds.GetRankingAsync(2, CancellationToken.None);

        Assert.Equal(2, top2.Count);
        Assert.Equal(firstId, top2[0].GuildId);
        Assert.Equal(secondId, top2[1].GuildId);
        Assert.True(top2[0].RankNo < top2[1].RankNo);
        Assert.DoesNotContain(top2, r => r.GuildId == thirdId);
    }

    private async Task<int> CountGuildsWithPointsGreaterThanAsync(int points)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command =
            new SqlCommand("SELECT COUNT(*) FROM game.Guilds WHERE Points > @Points;", connection);
        command.Parameters.AddWithValue("Points", points);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task<int> CreateCharacterAsync()
    {
        var accountId = await _accounts.CreateAsync($"gldranktest-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"R{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
    }

    private static string NewGuildName()
    {
        return $"R{Guid.NewGuid():N}"[..10];
    }
}

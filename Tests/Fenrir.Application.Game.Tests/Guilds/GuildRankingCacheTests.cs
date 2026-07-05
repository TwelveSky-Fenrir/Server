using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Application.Game.Tests.Guilds;

public class GuildRankingCacheTests
{
    [Fact]
    public void Top_BeforeAnyRefresh_IsEmpty()
    {
        var cache = new GuildRankingCache();

        Assert.Empty(cache.Top);
    }

    [Fact]
    public async Task RefreshAsync_PopulatesTop_HighestPointsFirst()
    {
        var cache = new GuildRankingCache();
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, "Aesir", 100));
        repository.Seed(Guild(2, "Vanir", 900));

        await cache.RefreshAsync(repository, CancellationToken.None);

        Assert.Equal(2, cache.Top.Length);
        Assert.Equal("Vanir", cache.Top[0].Name);
        Assert.Equal(900, cache.Top[0].Points);
        Assert.Equal("Aesir", cache.Top[1].Name);
    }

    [Fact]
    public async Task RefreshAsync_ReplacesThePreviousSnapshotEntirely()
    {
        var cache = new GuildRankingCache();
        var repository = new FakeGuildRepository();
        repository.Seed(Guild(1, "Aesir", 100));
        await cache.RefreshAsync(repository, CancellationToken.None);
        Assert.Single(cache.Top);

        repository.Seed(Guild(2, "Vanir", 50));
        await cache.RefreshAsync(repository, CancellationToken.None);

        Assert.Equal(2, cache.Top.Length);
    }

    private static GuildSummaryDto Guild(int guildId, string name, int points)
    {
        return new GuildSummaryDto(guildId, name, Grade: 1, MasterCharacterId: null, Points: points,
            BuffType: 0, BuffState: 0, BuffTime: 0, BuffTimeForDiff: 0, Logo: 0,
            CreatedAtUtc: DateTime.UtcNow, MemberCount: 1);
    }
}

using System.Collections.ObjectModel;
using Fenrir.Application.Game.Services.Guilds;
using Fenrir.Data.Abstractions.Guilds;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Guilds;

public sealed class FourGuildScoringRecomputeHostTests
{
    [Fact]
    public async Task RunOnceAsync_ForwardsIntoRecomputeAsync_PublishingStandings()
    {
        var repository = new FakeFourGuildScoringRepository
        {
            Leaderboard = [new GuildRankingRowDto(3, "Alpha", 40)]
        };
        var scoring = new FourGuildScoringService(repository, NullLogger<FourGuildScoringService>.Instance);
        var host = new FourGuildScoringRecomputeHost(scoring, NullLogger<FourGuildScoringRecomputeHost>.Instance);

        await host.RunOnceAsync(CancellationToken.None);

        Assert.Collection(scoring.CurrentStandings,
            s => Assert.Equal(new FourGuildStanding(3, "Alpha", 40), s));
    }

    [Fact]
    public async Task RunOnceAsync_RepositoryFault_NeverThrows()
    {
        var repository = new FakeFourGuildScoringRepository
        {
            LeaderboardThrow = new InvalidOperationException("simulated leaderboard read failure")
        };
        var scoring = new FourGuildScoringService(repository, NullLogger<FourGuildScoringService>.Instance);
        var host = new FourGuildScoringRecomputeHost(scoring, NullLogger<FourGuildScoringRecomputeHost>.Instance);

        var exception = await Record.ExceptionAsync(() => host.RunOnceAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    private sealed class FakeFourGuildScoringRepository : IFourGuildScoringRepository
    {
        public List<GuildRankingRowDto> Leaderboard { get; init; } = [];
        public Exception? LeaderboardThrow { get; set; }

        public ValueTask AddPointsAsync(int guildId, int delta, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetLeaderboardAsync(int count, CancellationToken ct)
        {
            if (LeaderboardThrow is { } ex)
                throw ex;

            return ValueTask.FromResult(new ReadOnlyCollection<GuildRankingRowDto>(Leaderboard));
        }
    }
}

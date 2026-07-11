using System.Collections.ObjectModel;
using Fenrir.Application.Game.Services.Guilds;
using Fenrir.Data.Abstractions.Guilds;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Guilds;

public class FourGuildScoringServiceTests
{
    private static FourGuildScoringService Create(IFourGuildScoringRepository repository)
    {
        return new FourGuildScoringService(repository, NullLogger<FourGuildScoringService>.Instance);
    }

    [Fact]
    public async Task AccrueKillPoint_CreditsExactlyPlusOneForTheGivenGuild()
    {
        var repository = new FakeFourGuildScoringRepository();
        var service = Create(repository);

        await service.AccrueKillPointAsync(77, CancellationToken.None);

        Assert.Equal((77, FourGuildScoringService.KillPointDelta), repository.LastAddPoints);
        Assert.Equal(1, FourGuildScoringService.KillPointDelta);
    }

    [Fact]
    public async Task AccrueKillPoint_SwallowsRepositoryFault_NeverPropagatesIntoTheKillPipeline()
    {
        var repository = new FakeFourGuildScoringRepository
        {
            AddPointsThrow = new InvalidOperationException("simulated accrual failure")
        };
        var service = Create(repository);

        var exception = await Record.ExceptionAsync(() => service.AccrueKillPointAsync(77, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Recompute_RequestsTopThree_AndPublishesTheStandings()
    {
        var repository = new FakeFourGuildScoringRepository
        {
            Leaderboard =
            [
                new GuildRankingRowDto(3, "Alpha", 40),
                new GuildRankingRowDto(9, "Bravo", 25),
                new GuildRankingRowDto(1, "Cadre", 5)
            ]
        };
        var service = Create(repository);

        await service.RecomputeAsync(CancellationToken.None);

        Assert.Equal(FourGuildScoringService.LeaderboardSize, repository.LastLeaderboardCount);
        Assert.Equal(3, FourGuildScoringService.LeaderboardSize);
        Assert.Collection(service.CurrentStandings,
            s => Assert.Equal(new FourGuildStanding(3, "Alpha", 40), s),
            s => Assert.Equal(new FourGuildStanding(9, "Bravo", 25), s),
            s => Assert.Equal(new FourGuildStanding(1, "Cadre", 5), s));
    }

    [Fact]
    public void CurrentStandings_IsEmpty_BeforeAnyRecompute()
    {
        var service = Create(new FakeFourGuildScoringRepository());

        Assert.Empty(service.CurrentStandings);
    }

    [Fact]
    public async Task Recompute_RepositoryFault_LeavesPreviouslyPublishedStandingsInPlace()
    {
        var repository = new FakeFourGuildScoringRepository
        {
            Leaderboard = [new GuildRankingRowDto(3, "Alpha", 40)]
        };
        var service = Create(repository);
        await service.RecomputeAsync(CancellationToken.None);

        repository.LeaderboardThrow = new InvalidOperationException("simulated leaderboard read failure");
        await service.RecomputeAsync(CancellationToken.None);

        Assert.Collection(service.CurrentStandings,
            s => Assert.Equal(new FourGuildStanding(3, "Alpha", 40), s));
    }

    private sealed class FakeFourGuildScoringRepository : IFourGuildScoringRepository
    {
        public (int GuildId, int Delta)? LastAddPoints { get; private set; }
        public Exception? AddPointsThrow { get; set; }
        public List<GuildRankingRowDto> Leaderboard { get; init; } = [];
        public int? LastLeaderboardCount { get; private set; }
        public Exception? LeaderboardThrow { get; set; }

        public ValueTask AddPointsAsync(int guildId, int delta, CancellationToken ct)
        {
            if (AddPointsThrow is { } ex)
                throw ex;

            LastAddPoints = (guildId, delta);
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetLeaderboardAsync(int count, CancellationToken ct)
        {
            LastLeaderboardCount = count;
            if (LeaderboardThrow is { } ex)
                throw ex;

            return ValueTask.FromResult(new ReadOnlyCollection<GuildRankingRowDto>(Leaderboard));
        }
    }
}

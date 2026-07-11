using System.Collections.ObjectModel;
using Fenrir.Application.Game.Services.Guilds;
using Fenrir.Data.Abstractions.Guilds;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Guilds;

// C17 Part B1 wiring: FourGuildKillPointRelayHost is the non-blocking Domain-facing producer half
// (Enqueue -- what Zone.KillFeed.cs's RecordEnemyKillForFeed calls through IFourGuildKillPointQueue) plus the
// BackgroundService drain loop that forwards each entry into FourGuildScoringService.AccrueKillPointAsync.
// Zone (Domain) never sees this concrete host or FourGuildScoringService directly -- only the narrow
// IFourGuildKillPointQueue surface -- so these tests exercise the host end-to-end (Enqueue -> drain ->
// repository call) rather than mocking the drain loop away.
public sealed class FourGuildKillPointRelayHostTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            if (condition())
                return true;
            await Task.Delay(10);
        }

        return condition();
    }

    [Fact]
    public async Task Enqueue_WhileRunning_DrainsIntoAccrueKillPoint()
    {
        var repository = new FakeFourGuildScoringRepository();
        var scoring = new FourGuildScoringService(repository, NullLogger<FourGuildScoringService>.Instance);
        await using var host =
            new FourGuildKillPointRelayHost(scoring, NullLogger<FourGuildKillPointRelayHost>.Instance);

        await host.StartAsync(CancellationToken.None);

        Assert.True(host.Enqueue(42));

        Assert.True(await WaitUntilAsync(() => repository.LastAddPoints is not null, BoundedWait));
        Assert.Equal((42, FourGuildScoringService.KillPointDelta), repository.LastAddPoints);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Enqueue_MultipleGuilds_EachForwardedInOrder()
    {
        var repository = new FakeFourGuildScoringRepository();
        var scoring = new FourGuildScoringService(repository, NullLogger<FourGuildScoringService>.Instance);
        await using var host =
            new FourGuildKillPointRelayHost(scoring, NullLogger<FourGuildKillPointRelayHost>.Instance);

        await host.StartAsync(CancellationToken.None);

        Assert.True(host.Enqueue(1));
        Assert.True(host.Enqueue(2));
        Assert.True(host.Enqueue(3));

        Assert.True(await WaitUntilAsync(() => repository.AllAddedGuildIds.Count == 3, BoundedWait));
        Assert.Equal([1, 2, 3], repository.AllAddedGuildIds);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Enqueue_BeforeStart_StillAccepted_NoExceptionOrBlocking()
    {
        var repository = new FakeFourGuildScoringRepository();
        var scoring = new FourGuildScoringService(repository, NullLogger<FourGuildScoringService>.Instance);
        var host = new FourGuildKillPointRelayHost(scoring, NullLogger<FourGuildKillPointRelayHost>.Instance);

        // Enqueue is a plain bounded-channel TryWrite -- safe and non-blocking even before ExecuteAsync ever
        // runs (matching Zone's own tick thread, which must never wait on a hosted service's own startup).
        Assert.True(host.Enqueue(7));
    }

    private sealed class FakeFourGuildScoringRepository : IFourGuildScoringRepository
    {
        public List<int> AllAddedGuildIds { get; } = [];
        public (int GuildId, int Delta)? LastAddPoints { get; private set; }

        public ValueTask AddPointsAsync(int guildId, int delta, CancellationToken ct)
        {
            LastAddPoints = (guildId, delta);
            AllAddedGuildIds.Add(guildId);
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetLeaderboardAsync(int count, CancellationToken ct)
        {
            return ValueTask.FromResult(new ReadOnlyCollection<GuildRankingRowDto>(new List<GuildRankingRowDto>()));
        }
    }
}

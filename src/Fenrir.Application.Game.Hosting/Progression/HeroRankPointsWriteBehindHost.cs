using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.Progression;

public sealed class HeroRankPointsWriteBehindHost(
    HeroRankPointAccumulator heroRankPoints,
    IHeroRankingRepository heroRankings) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await heroRankPoints.FlushDirtyAsync(heroRankings, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await heroRankPoints.FlushDirtyAsync(heroRankings, CancellationToken.None).ConfigureAwait(false);
    }
}

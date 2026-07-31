using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.Progression;

public sealed class HeroRankPointsWriteBehindHost(
    HeroRankPointAccumulator heroRankPoints,
    IHeroRankingRepository heroRankings,
    IOptions<GameServerOptions> options,
    ILogger<HeroRankPointsWriteBehindHost> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.WorldStateAuthority == WorldStateAuthorityMode.Center)
        {
            logger.LogInformation(
                "HeroRankPointsWriteBehindHost inert: WorldStateAuthority=Center (CenterServer owns hero-rank writes)");
            return;
        }

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

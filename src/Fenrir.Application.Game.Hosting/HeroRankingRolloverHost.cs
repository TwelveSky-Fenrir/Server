using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class HeroRankingRolloverHost(
    IHeroRankingRepository heroRankings,
    HeroRankPointAccumulator heroRankPoints,
    ZoneRegistry zones,
    FavoredTribeRankBonusLadderService favoredTribeLadder,
    IOptions<GameServerOptions> options,
    ILogger<HeroRankingRolloverHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(opts.HeroRankingRolloverCheckIntervalMinutes));

        do
        {
            try
            {
                // Force this shard's pending points into PeriodKind=0 before the DB snapshots/clears it --
                // HeroRankPointsWriteBehindHost's own 2s timer runs independently and is not otherwise synchronized.
                await heroRankPoints.FlushDirtyAsync(heroRankings, stoppingToken).ConfigureAwait(false);

                if (await heroRankings.RolloverIfDueAsync(stoppingToken).ConfigureAwait(false))
                {
                    logger.LogInformation(
                        "Hero ranking rollover: Current period flipped into Previous (7-day sentinel elapsed)");

                    NotifyConnectedSessions();

                    await favoredTribeLadder.RotateToNextFavoredTribeAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Hero ranking rollover check failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private void NotifyConnectedSessions()
    {
        foreach (var zone in zones.Zones)
            zone.PostHeroRankingRolloverReset();
    }
}

using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class HeroRankingRolloverHost(
    IHeroRankingRepository heroRankings,
    ZoneRegistry zones,
    IOptions<GameServerOptions> options,
    ILogger<HeroRankingRolloverHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        if (opts.WorldStateAuthority == WorldStateAuthorityMode.Center)
        {
            // Le CenterServer possède le rollover hero-rank (write) ; la notification client du reset devra
            // arriver par fan-out Center (dépendance §4c du Lot 5). Host inerte côté shard.
            logger.LogInformation("HeroRankingRolloverHost inert: WorldStateAuthority=Center");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(opts.HeroRankingRolloverCheckIntervalMinutes));

        do
        {
            try
            {
                if (await heroRankings.RolloverIfDueAsync(stoppingToken).ConfigureAwait(false))
                {
                    logger.LogInformation(
                        "Hero ranking rollover: Current period flipped into Previous (7-day sentinel elapsed)");

                    NotifyConnectedSessions();
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

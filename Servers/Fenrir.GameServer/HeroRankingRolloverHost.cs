using Fenrir.Application.Game;
using Fenrir.Data.Progression;
using Microsoft.Extensions.Options;

namespace Fenrir.GameServer;

/// <summary>
///     Polls whether the weekly hero-ranking Current-&gt;Previous rollover (<c>game.usp_HeroRanking_Rollover</c>)
///     is due. All the actual gating (the 7-day sentinel, the flip itself) lives in the proc, which is safe
///     to call redundantly and concurrently from every shard -- this host does not need to coordinate with
///     any other GameServer process, unlike <see cref="ShardPartitionGuard" />.
/// </summary>
public sealed class HeroRankingRolloverHost(
    IHeroRankingRepository heroRankings,
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
                if (await heroRankings.RolloverIfDueAsync(stoppingToken).ConfigureAwait(false))
                    logger.LogInformation(
                        "Hero ranking rollover: Current period flipped into Previous (7-day sentinel elapsed)");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed check just delays the flip to the next tick -- never worth crashing the GameServer.
                logger.LogError(ex, "Hero ranking rollover check failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}

using Fenrir.Application.Game.Domain.Progression;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.Progression;

/// <summary>
///     Periodic write-behind for <see cref="HeroRankPointAccumulator" />, same "skip the round trip when
///     nothing changed" shape as <see cref="TowerWarWriteBehindHost" />.
/// </summary>
public sealed class HeroRankPointsWriteBehindHost(
    HeroRankPointAccumulator heroRankPoints,
    IHeroRankingRepository heroRankings) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

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
            // Expected on shutdown -- fall through to the final flush below.
        }

        // Best-effort final flush so a graceful shutdown doesn't lose the last few seconds of pending grants.
        // FlushDirtyAsync itself already logs and swallows failures -- never let shutdown throw.
        await heroRankPoints.FlushDirtyAsync(heroRankings, CancellationToken.None).ConfigureAwait(false);
    }
}

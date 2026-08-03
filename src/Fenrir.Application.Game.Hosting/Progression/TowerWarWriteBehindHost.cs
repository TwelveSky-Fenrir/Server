using Fenrir.Application.Game.Domain.Progression;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.Progression;

public sealed class TowerWarWriteBehindHost(TowerWarState towerWar, ITowerRepository towers) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await towerWar.FlushDirtyAsync(towers, stoppingToken).ConfigureAwait(false);
                await towerWar.ReconcileAsync(towers, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }

        await towerWar.FlushDirtyAsync(towers, CancellationToken.None).ConfigureAwait(false);
    }
}

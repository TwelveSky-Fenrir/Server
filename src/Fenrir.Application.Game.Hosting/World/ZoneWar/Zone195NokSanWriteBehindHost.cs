using Fenrir.Application.Game.Services.ZoneWar;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class Zone195NokSanWriteBehindHost(Zone195NokSanStateService stateService) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await stateService.FlushIfDirtyAsync(stoppingToken).ConfigureAwait(false);
                await stateService.ReconcileAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }

        await stateService.FlushIfDirtyAsync(CancellationToken.None).ConfigureAwait(false);
    }
}

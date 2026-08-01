using Fenrir.Application.Game.Domain.Commerce;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.Commerce;

public sealed class CommerceCatalogRefreshHost(
    CommerceCatalogCache cache,
    IWorldDataRepository repository,
    ILogger<CommerceCatalogRefreshHost> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await RefreshSafeAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshSafeAsync(CancellationToken ct)
    {
        try
        {
            await cache.RefreshAllAsync(repository, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cash/blood catalog reload failed -- keeping the previous snapshot");
        }
    }
}

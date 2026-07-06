using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.Commerce;

/// <summary>
///     Keeps <see cref="CommerceCatalogCache" /> warm after boot. The initial fill happens once, synchronously,
///     at Program.cs startup (mirroring <c>GuildRankingRefreshHost</c>/<c>WorldStateService.InitializeAsync</c>)
///     so the very first CZ_GET_CASH_ITEM_INFO_SEND/CZ_DEMAND_BLOOD_MARK_SEND of the shard's life doesn't see
///     an empty catalog; this host only covers every reload after that.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/BuildEU33/ServerInfo.ini:107-115 (the catalog-owning process's own ~1 s logic-tick
///     interval in production) ; Server/ts25extra/S07_MyGame01.cpp:88-131 (the per-tick call site -- runs every
///     tick, not every 10th, see <see cref="CommerceCatalogCache" />'s own remarks).
/// </remarks>
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
            // Expected on shutdown.
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
            // CommerceCatalogCache.RefreshCashCatalogAsync/RefreshBloodCatalogAsync already swallow and log
            // their own expected failures -- this is a last-resort net for anything unexpected, matching
            // GuildRankingRefreshHost's own posture.
            logger.LogError(ex, "Cash/blood catalog reload failed -- keeping the previous snapshot");
        }
    }
}

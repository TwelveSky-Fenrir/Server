using Fenrir.Application.Game.Domain.Guilds;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.Guilds;

/// <summary>
///     Keeps <see cref="GuildRankingCache" /> warm after boot. The initial fill happens once, synchronously,
///     at Program.cs startup (mirroring <c>WorldStateService.InitializeAsync</c>) so the very first op12/op20 of
///     the shard's life doesn't broadcast an empty ranking board; this host only covers every refresh after that.
/// </summary>
public sealed class GuildRankingRefreshHost(
    IGuildRepository guilds,
    GuildRankingCache cache,
    ILogger<GuildRankingRefreshHost> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

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
            await cache.RefreshAsync(guilds, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Guild ranking refresh failed -- keeping the previous snapshot");
        }
    }
}

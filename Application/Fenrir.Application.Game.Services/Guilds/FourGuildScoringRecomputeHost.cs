using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Guilds;

public sealed class FourGuildScoringRecomputeHost(
    FourGuildScoringService scoring,
    ILogger<FourGuildScoringRecomputeHost> logger) : BackgroundService
{
    public static readonly TimeSpan RecomputeInterval = TimeSpan.FromSeconds(10);

    public async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await scoring.RecomputeAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Four-guild leaderboard recompute tick failed");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(RecomputeInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}

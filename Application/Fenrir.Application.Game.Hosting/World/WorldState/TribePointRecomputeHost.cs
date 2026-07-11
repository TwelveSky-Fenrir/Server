using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.WorldState;

public sealed class TribePointRecomputeHost(
    TribePointLevelRecomputeService levelRecompute,
    FavoredTribeRankBonusLadderService ladder,
    ILogger<TribePointRecomputeHost> logger) : BackgroundService
{

        public const int TickGate = 6;

        public static readonly TimeSpan CenterTick = TimeSpan.FromSeconds(1);

    private int _tickCount;

        public Task RunBootRecomputeAsync(CancellationToken ct)
    {
        return RunOnceAsync(ct);
    }

        public async Task TickAsync(CancellationToken ct)
    {
        _tickCount++;
        if (_tickCount % TickGate != 0)
            return;

        await RunOnceAsync(ct).ConfigureAwait(false);
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await levelRecompute.RecomputeAsync(ct).ConfigureAwait(false);
        await ladder.TickIfPendingAsync(ct).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunBootRecomputeAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(CenterTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    await TickAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Tribe point recompute tick failed");
                }
        }
        catch (OperationCanceledException)
        {
        }
    }
}

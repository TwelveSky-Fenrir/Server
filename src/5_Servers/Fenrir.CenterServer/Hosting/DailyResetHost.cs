using Fenrir.Cluster.WorldState;

namespace Fenrir.CenterServer.Hosting;

public sealed class DailyResetHost(
    ICenterDailyRewardReset rewardReset,
    ICenterLinkBroadcaster broadcaster,
    ILogger<DailyResetHost> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private bool _firedToday;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await EvaluateAsync(DateTimeOffset.Now, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Daily-reset evaluation failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task EvaluateAsync(DateTimeOffset localNow, CancellationToken ct)
    {
        if (localNow is { Hour: 0, Minute: >= 2 })
        {
            _firedToday = false;
            return;
        }

        if (_firedToday || localNow is not { Hour: 0, Minute: 1 })
            return;

        _firedToday = true;

        var isMonday = localNow.DayOfWeek == DayOfWeek.Monday;
        await rewardReset.ResetDailyRewardClaimsAsync(isMonday, ct).ConfigureAwait(false);

        var payload = new byte[CenterWorldEventSorts.PayloadSize];
        await broadcaster.BroadcastWorldEventAsync(CenterWorldEventSorts.DailyReset, payload, ct).ConfigureAwait(false);

        logger.LogInformation("Daily reset fired (Monday weekly clear: {IsMonday}); broadcast sort 1600", isMonday);
    }
}

using Fenrir.Cluster.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.CenterServer.Hosting;

/// <summary>
///     The once-daily reset cadence: at local wall-clock 00:01 it fires once (re-armed at 00:02), resets the
///     reward-claim state for all members through a repository (Bug 4), additionally clears the weekly reward-day
///     counter on Mondays, and broadcasts the daily-reset notice (sort 1600) to every zone.
/// </summary>
/// <remarks>
///     Reimplemented from the daily reset (Server/ts25center/S07_MyGame01.cpp:218-238). The trigger is the real
///     OS local calendar time (hour/minute/weekday), never a tick counter. CORRECTS Bug 4: the member reset goes
///     through <see cref="ICenterDailyRewardReset" /> (a repository/proc), never the literal <c>MemberInfo</c>
///     table name the legacy hard-coded.
///     <para>
///         DORMANCY NOTE: the GameServer's own <c>DailyResetBroadcaster</c> also emits sort 1600 this lot; the
///         Center is the future authoritative emitter. Nothing shard-side is removed.
///     </para>
/// </remarks>
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
        // Re-arm window at 00:02 so the next 00:01 fires again tomorrow.
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

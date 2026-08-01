using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Hosting;

public sealed class AccountSessionReapHost(
    IAccountSessionRepository accountSessions,
    ILogger<AccountSessionReapHost> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await ReapOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Account session reap sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async ValueTask ReapOnceAsync(CancellationToken ct)
    {
        var reaped = await accountSessions.ReapStaleAsync(ct).ConfigureAwait(false);

        foreach (var row in reaped)
            logger.LogWarning(
                "Reaped stale runtime.AccountSessions row for account {AccountId} (ServerKind={ServerKind}) -- LastRefreshedUtc exceeded the 6-minute staleness window",
                row.AccountId, (AccountSessionServerKind)row.ServerKind);
    }
}

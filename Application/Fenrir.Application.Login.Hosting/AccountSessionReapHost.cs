using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Hosting;

/// <summary>
///     Single unsharded timer (Login side only -- see <see cref="IAccountSessionRepository.ReapStaleAsync" />'s
///     remarks) that deletes every <c>runtime.AccountSessions</c> row whose <c>LastRefreshedUtc</c> is older than
///     6 minutes -- the backstop for a process that crashed/was killed without running its own teardown path
///     (<c>LoginConnectionHost</c>'s/<c>GameConnectionHost</c>'s <c>ClearIfOwnerAsync</c> calls).
/// </summary>
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
                // A missed sweep just delays reaping a crashed process's stale row -- never worth crashing over.
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

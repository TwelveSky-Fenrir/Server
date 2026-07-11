using Fenrir.Application.Login.Domain;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting;

public sealed class AccountSessionLivenessHost(
    SessionRegistry registry,
    IAccountSessionRepository accountSessions,
    IOptions<LoginServerOptions> options,
    ILogger<AccountSessionLivenessHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.AccountSessionRefreshIntervalSeconds));

        do
        {
            try
            {
                await RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Account session liveness refresh failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async ValueTask RefreshAsync(CancellationToken ct)
    {
        var accountIds = registry.SnapshotAssociatedAccountIds();
        if (accountIds.IsEmpty)
            return;

        var ids = accountIds.Select(id => (int)id).ToArray();

        _ = await accountSessions.RefreshAndGetKickedAsync(AccountSessionServerKind.Login, null, ids, ct)
            .ConfigureAwait(false);
    }
}

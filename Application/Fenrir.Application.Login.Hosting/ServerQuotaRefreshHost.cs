using Fenrir.Application.Login.Domain;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Hosting;

public sealed class ServerQuotaRefreshHost(
    LoginCapacityState state,
    IServerQuotaRepository quota,
    IAccountSessionRepository accountSessions,
    ILogger<ServerQuotaRefreshHost> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

        public async ValueTask InitializeAsync(CancellationToken ct)
    {
        var maxPlayers = await quota.GetMaxPlayersAsync(ct).ConfigureAwait(false);
        state.SetMaxPlayers(maxPlayers);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            await RefreshOnceAsync(stoppingToken).ConfigureAwait(false);
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

        public async ValueTask RefreshOnceAsync(CancellationToken ct)
    {
        try
        {
            var maxPlayers = await quota.GetMaxPlayersAsync(ct).ConfigureAwait(false);
            state.SetMaxPlayers(maxPlayers);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to refresh admin.ServerQuota.MaxPlayers; keeping the previous value ({MaxPlayers})",
                state.MaxPlayers);
        }

        if (state.MaxPlayers == 0)
            return;

        try
        {
            var currentPlayers = await accountSessions.GetActiveSessionCountAsync(ct).ConfigureAwait(false);
            state.SetCurrentPlayers(currentPlayers);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to refresh the cluster-wide active session count; keeping the previous value ({CurrentPlayers})",
                state.CurrentPlayers);
        }
    }
}

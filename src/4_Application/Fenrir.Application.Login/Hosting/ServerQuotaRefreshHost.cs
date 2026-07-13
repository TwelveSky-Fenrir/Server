using Fenrir.Domain.Login;
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
        var row = await quota.GetAsync(ct).ConfigureAwait(false);
        state.SetMaxPlayers(row.MaxPlayers);
        state.SetGagePlayers(row.GagePlayerNum);
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
            var row = await quota.GetAsync(ct).ConfigureAwait(false);
            state.SetMaxPlayers(row.MaxPlayers);
            state.SetGagePlayers(row.GagePlayerNum);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to refresh admin.ServerQuota; keeping the previous values (MaxPlayers {MaxPlayers}, GagePlayerNum {GagePlayerNum})",
                state.MaxPlayers, state.GagePlayers);
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

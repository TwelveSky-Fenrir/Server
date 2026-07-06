using Fenrir.Application.Login.Domain;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Hosting;

/// <summary>
///     Keeps <see cref="LoginCapacityState" /> continuously fresh for the CL_LOGIN_SEND maintenance-lockdown
///     and server-full quota gates (<c>LoginService.LoginAsync</c>) -- a durable-store-backed value refreshed
///     on a recurring ~1s timer plus one synchronous read at process startup, never a value bound once from
///     config and left fixed for the process lifetime.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S07_MyGame01.cpp:4-29 (<c>MyGame::Init</c> -- one-time synchronous startup
///     read, fatal on failure) and :37-85 (<c>MyGame::Logic</c> -- the recurring tick: configured max re-read
///     every tick unconditionally at line 79, current count re-read only when the just-refreshed max isn't
///     signaling maintenance at lines 80-84; the recurring re-read's own success/failure is never checked by
///     this caller, matching this host's independent try/catch per sub-step below). Server/ts25login/
///     S02_MyServer.cpp:147-153 -- the one-time read runs strictly before the recurring timer is armed.
///     Server/BuildEU33/ServerInfo.ini:117-126 -- the ~1000ms tick interval this host's timer mirrors.
/// </remarks>
public sealed class ServerQuotaRefreshHost(
    LoginCapacityState state,
    IServerQuotaRepository quota,
    IAccountSessionRepository accountSessions,
    ILogger<ServerQuotaRefreshHost> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Server/ts25login/S02_MyServer.cpp:147-153 / S07_MyGame01.cpp:4-29 (<c>MyGame::Init</c>): the
    ///     one-time, synchronous startup read. Deliberately left uncaught -- a failure here must abort
    ///     LoginServer startup (called from Program.cs before <c>host.RunAsync()</c>), matching the legacy
    ///     fatal-startup-error treatment, unlike the recurring re-read below.
    /// </summary>
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

    /// <summary>
    ///     One recurring-timer tick. The configured maximum is always attempted first; the current count is
    ///     attempted afterward only if the (possibly still-stale, on a failed max refresh) resulting
    ///     <see cref="LoginCapacityState.MaxPlayers" /> isn't 0 -- mirroring S07_MyGame01.cpp:79-84's
    ///     unconditional <c>mDB.GetMaxUser()</c> call followed by an <c>if (mMaxPlayerNum &gt; 0)</c> guard that
    ///     reads whatever value is currently held, stale or fresh. Each sub-step swallows its own failure
    ///     independently and just leaves the previous snapshot in place -- a transient store outage never
    ///     forces maintenance mode nor crashes this host.
    /// </summary>
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

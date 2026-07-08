using Fenrir.Application.Login.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting;

/// <summary>
///     Wall-clock poll driver for <see cref="LoginSessionLivenessSweep" /> (Server/ts25login/S07_MyGame01.cpp:37-73).
///     Runs unconditionally, gated on nothing else -- an abandoned raw connection can stall before any
///     authentication or char-select step is ever reached, so this must not wait on any other Login subsystem.
/// </summary>
public sealed class LoginSessionLivenessSweepHost(
    LoginSessionLivenessSweep sweep,
    IOptions<LoginServerOptions> options,
    ILogger<LoginSessionLivenessSweepHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IdleSweepIntervalSeconds));

        do
        {
            try
            {
                sweep.Sweep(DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed sweep just delays an idle disconnect by one cycle -- never worth crashing the LoginServer.
                logger.LogError(ex, "Login session liveness sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}

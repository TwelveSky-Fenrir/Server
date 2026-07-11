using Fenrir.Application.Login.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting;

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
                logger.LogError(ex, "Login session liveness sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}

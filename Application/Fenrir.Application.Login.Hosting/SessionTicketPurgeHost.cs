using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Hosting;

public sealed class SessionTicketPurgeHost(
    ISessionTicketRepository tickets,
    ILogger<SessionTicketPurgeHost> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await PurgeOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Session ticket purge sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async ValueTask PurgeOnceAsync(CancellationToken ct)
    {
        await tickets.PurgeExpiredAsync(ct).ConfigureAwait(false);
    }
}

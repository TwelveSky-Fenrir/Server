using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Hosting;

/// <summary>
///     Single unsharded timer (Login side only -- ticket creation itself is Login-owned via
///     <c>ZoneTransferService.RequestZoneTransferAsync</c>) that deletes every <c>runtime.SessionTickets</c> row
///     whose <c>ExpiresAtUtc</c> has already passed -- the backstop for a zone-transfer ticket whose handshake
///     never completed (client crash, dropped connection between Login and Game, or a client that simply never
///     retries) and therefore was never removed by <c>usp_SessionTicket_Consume</c>. Mirrors
///     <see cref="AccountSessionReapHost" />, the directly analogous job for the sibling
///     <c>runtime.AccountSessions</c> table: same fixed interval, same catch-log-continue failure handling, since
///     <c>runtime.SessionTickets</c> is SCHEMA_ONLY memory-optimized and has no cleanup of its own besides this
///     sweep and the create/consume paths' own supersede-on-recreate/always-delete-on-consume behavior.
/// </summary>
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
                // A missed sweep just delays reaping an abandoned ticket -- never worth crashing over.
                logger.LogError(ex, "Session ticket purge sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async ValueTask PurgeOnceAsync(CancellationToken ct)
    {
        await tickets.PurgeExpiredAsync(ct).ConfigureAwait(false);
    }
}

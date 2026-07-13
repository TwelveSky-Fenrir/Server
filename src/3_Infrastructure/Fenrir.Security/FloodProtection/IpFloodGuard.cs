using System.Collections.Concurrent;
using Fenrir.Security.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Security.FloodProtection;

public sealed class IpFloodGuard(
    int maxConnectionsPerIp,
    int maxProtocolViolationsPerIpPerHour,
    Func<string, CancellationToken, ValueTask> blockIpAsync,
    IFloodKickSink kickSink,
    Func<DateTime>? utcNowProvider = null,
    ILogger<IpFloodGuard>? logger = null)
{
    private readonly ConcurrentDictionary<string, int> _connectionCounts = new();
    private readonly Func<DateTime> _utcNowProvider = utcNowProvider ?? DefaultUtcNowProvider;
    private readonly ConcurrentDictionary<string, ViolationWindow> _violationWindows = new();

    private static DateTime DefaultUtcNowProvider()
    {
        return DateTime.UtcNow;
    }

    public async ValueTask<bool> TryAcquireConnectionAsync(string ipAddress, CancellationToken ct)
    {
        var count = _connectionCounts.AddOrUpdate(ipAddress, 1, static (_, existing) => existing + 1);

        if (count <= maxConnectionsPerIp)
            return true;

        await BlockAndKickAsync(ipAddress, ct).ConfigureAwait(false);
        return false;
    }

    public void ReleaseConnection(string ipAddress)
    {
        _connectionCounts.AddOrUpdate(ipAddress, 0, static (_, existing) => Math.Max(0, existing - 1));

        _connectionCounts.TryRemove(new KeyValuePair<string, int>(ipAddress, 0));
    }

    public async ValueTask RecordProtocolViolationAsync(string ipAddress, CancellationToken ct)
    {
        var currentHour = _utcNowProvider().Ticks / TimeSpan.TicksPerHour;

        var window = _violationWindows.AddOrUpdate(
            ipAddress,
            _ => new ViolationWindow(currentHour, 1),
            (_, existing) => existing.HourBucket == currentHour
                ? existing with { Count = existing.Count + 1 }
                : new ViolationWindow(currentHour, 1));

        if (window.Count < maxProtocolViolationsPerIpPerHour)
            return;

        await BlockAndKickAsync(ipAddress, ct).ConfigureAwait(false);
    }

    private async ValueTask BlockAndKickAsync(string ipAddress, CancellationToken ct)
    {
        try
        {
            await blockIpAsync(ipAddress, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Contrat F5 §2.4 : un échec de persistance est TOUJOURS avalé sans empêcher le kick — y compris
            // pendant l'arrêt gracieux (ct annulé). Un filtre `when (!ct.IsCancellationRequested)` sauterait
            // le kick sous annulation, ce que l'invariant interdit.
            logger?.IpBlockPersistFailed(ex, ipAddress);
        }

        // Éjection via le port (la boucle snapshot+Abort vit dans SessionRegistry, côté Network).
        var kicked = kickSink.KickByRemoteAddress(ipAddress);
        logger?.IpBlocked(ipAddress, kicked);
    }

    private readonly record struct ViolationWindow(long HourBucket, int Count);
}

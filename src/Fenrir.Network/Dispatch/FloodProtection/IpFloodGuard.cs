using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.FloodProtection;

public sealed class IpFloodGuard(
    int maxConnectionsPerIp,
    int maxProtocolViolationsPerIpPerHour,
    Func<string, CancellationToken, ValueTask> blockIpAsync,
    IFloodKickSink kickSink,
    Func<DateTime>? utcNowProvider = null,
    ILogger<IpFloodGuard>? logger = null,
    Func<string, CancellationToken, ValueTask<bool>>? isOperatorAllowlistedAsync = null)
{
    private readonly ConcurrentDictionary<string, int> _connectionCounts = new();
    private readonly ConcurrentDictionary<string, byte> _escalatedIps = new();
    private readonly ConcurrentDictionary<string, long> _lastEscalationHour = new();
    private readonly Func<DateTime> _utcNowProvider = utcNowProvider ?? DefaultUtcNowProvider;
    private readonly ConcurrentDictionary<string, ViolationWindow> _violationWindows = new();

    private long _lastPrunedHourBucket = long.MinValue;

    private static DateTime DefaultUtcNowProvider()
    {
        return DateTime.UtcNow;
    }

    public async ValueTask<bool> TryAcquireConnectionAsync(string ipAddress, CancellationToken ct)
    {
        var count = _connectionCounts.AddOrUpdate(ipAddress, 1, static (_, existing) => existing + 1);

        if (count <= maxConnectionsPerIp)
            return true;

        if (_escalatedIps.TryAdd(ipAddress, 0))
            await BlockAndKickAsync(ipAddress, ct).ConfigureAwait(false);

        return false;
    }

    public void ReleaseConnection(string ipAddress)
    {
        var count = _connectionCounts.AddOrUpdate(ipAddress, 0, static (_, existing) => Math.Max(0, existing - 1));

        if (count <= maxConnectionsPerIp)
            _escalatedIps.TryRemove(ipAddress, out _);

        _connectionCounts.TryRemove(new KeyValuePair<string, int>(ipAddress, 0));
    }

    public async ValueTask RecordProtocolViolationAsync(string ipAddress, CancellationToken ct)
    {
        var currentHour = CurrentHourBucket();

        var window = _violationWindows.AddOrUpdate(
            ipAddress,
            static (_, hour) => new ViolationWindow(hour, 1),
            static (_, existing, hour) => existing.HourBucket == hour
                ? existing with { Count = existing.Count + 1 }
                : new ViolationWindow(hour, 1),
            currentHour);

        PruneStaleTrackingState(currentHour);

        if (window.Count < maxProtocolViolationsPerIpPerHour)
            return;

        await BlockAndKickAsync(ipAddress, ct).ConfigureAwait(false);
    }

    public void Forget(string ipAddress)
    {
        _escalatedIps.TryRemove(ipAddress, out _);
        _violationWindows.TryRemove(ipAddress, out _);
        _lastEscalationHour.TryRemove(ipAddress, out _);
        _connectionCounts.TryRemove(ipAddress, out _);
    }

    private long CurrentHourBucket()
    {
        return _utcNowProvider().Ticks / TimeSpan.TicksPerHour;
    }

    private bool TryClaimEscalation(string ipAddress, long currentHour)
    {
        while (true)
        {
            if (_lastEscalationHour.TryGetValue(ipAddress, out var recorded))
            {
                if (recorded == currentHour)
                    return false;

                if (_lastEscalationHour.TryUpdate(ipAddress, currentHour, recorded))
                    return true;

                continue;
            }

            if (_lastEscalationHour.TryAdd(ipAddress, currentHour))
                return true;
        }
    }

    private void PruneStaleTrackingState(long currentHour)
    {
        var lastPruned = Interlocked.Read(ref _lastPrunedHourBucket);

        if (lastPruned == currentHour ||
            Interlocked.CompareExchange(ref _lastPrunedHourBucket, currentHour, lastPruned) != lastPruned)
            return;

        foreach (var entry in _violationWindows)
            if (entry.Value.HourBucket < currentHour)
                _violationWindows.TryRemove(entry);

        foreach (var entry in _lastEscalationHour)
            if (entry.Value < currentHour)
                _lastEscalationHour.TryRemove(entry);
    }

    private async ValueTask BlockAndKickAsync(string ipAddress, CancellationToken ct)
    {
        var currentHour = CurrentHourBucket();

        if (!TryClaimEscalation(ipAddress, currentHour))
            return;

        PruneStaleTrackingState(currentHour);

        if (await IsOperatorAllowlistedAsync(ipAddress, ct).ConfigureAwait(false))
        {
            logger?.IpBlockSkippedForOperatorAllowlist(ipAddress);
            return;
        }

        try
        {
            await blockIpAsync(ipAddress, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.IpBlockPersistFailed(ex, ipAddress);
        }

        var kicked = kickSink.KickByRemoteAddress(ipAddress);
        logger?.IpBlocked(ipAddress, kicked);
    }

    private async ValueTask<bool> IsOperatorAllowlistedAsync(string ipAddress, CancellationToken ct)
    {
        if (isOperatorAllowlistedAsync is null)
            return false;

        try
        {
            return await isOperatorAllowlistedAsync(ipAddress, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.OperatorAllowlistLookupFailed(ex, ipAddress);
            return false;
        }
    }

    private readonly record struct ViolationWindow(long HourBucket, int Count);
}

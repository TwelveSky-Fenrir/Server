using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.FloodProtection;

public sealed class IpFloodGuard(
    int maxConnectionsPerIp,
    int maxProtocolViolationsPerIpPerHour,
    Func<string, CancellationToken, ValueTask> blockIpAsync,
    IFloodKickSink kickSink,
    Func<long>? monotonicTimestampProvider = null,
    ILogger<IpFloodGuard>? logger = null,
    Func<string, CancellationToken, ValueTask<bool>>? isOperatorAllowlistedAsync = null)
{
    private static readonly long ViolationWindowTicks = Stopwatch.Frequency * 3600;

    private static readonly long ReescalationIntervalTicks = Stopwatch.Frequency * 900;

    private static readonly long PersistRetryIntervalTicks = Stopwatch.Frequency * 30;

    private static readonly long PruneIntervalTicks = Stopwatch.Frequency * 300;

    private readonly ConcurrentDictionary<string, int> _connectionCounts = new();
    private readonly ConcurrentDictionary<string, byte> _escalatedIps = new();
    private readonly ConcurrentDictionary<string, long> _nextEscalationTimestamp = new();
    private readonly Func<long> _timestampProvider = monotonicTimestampProvider ?? Stopwatch.GetTimestamp;
    private readonly ConcurrentDictionary<string, ViolationWindow> _violationWindows = new();

    private long _nextPruneTimestamp;

    public async ValueTask<bool> TryAcquireConnectionAsync(string ipAddress, CancellationToken ct)
    {
        var count = _connectionCounts.AddOrUpdate(ipAddress, 1, static (_, existing) => existing + 1);

        if (count <= maxConnectionsPerIp)
            return true;

        if (!_escalatedIps.ContainsKey(ipAddress))
            await EscalateAsync(ipAddress, ct).ConfigureAwait(false);

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
        var now = _timestampProvider();

        var window = _violationWindows.AddOrUpdate(
            ipAddress,
            static (_, timestamp) => new ViolationWindow(timestamp, 1),
            static (_, existing, timestamp) => timestamp - existing.WindowStartTimestamp > ViolationWindowTicks
                ? new ViolationWindow(timestamp, 1)
                : existing with { Count = existing.Count + 1 },
            now);

        PruneStaleTrackingState(now);

        if (window.Count < maxProtocolViolationsPerIpPerHour)
            return;

        await EscalateAsync(ipAddress, ct).ConfigureAwait(false);
    }

    public void Forget(string ipAddress)
    {
        _escalatedIps.TryRemove(ipAddress, out _);
        _violationWindows.TryRemove(ipAddress, out _);
        _nextEscalationTimestamp.TryRemove(ipAddress, out _);
        _connectionCounts.TryRemove(ipAddress, out _);
    }

    private bool TryClaimEscalation(string ipAddress, long now, out long claimedDeadline)
    {
        claimedDeadline = now + ReescalationIntervalTicks;

        while (true)
        {
            if (_nextEscalationTimestamp.TryGetValue(ipAddress, out var recorded))
            {
                if (now < recorded)
                    return false;

                if (_nextEscalationTimestamp.TryUpdate(ipAddress, claimedDeadline, recorded))
                    return true;

                continue;
            }

            if (_nextEscalationTimestamp.TryAdd(ipAddress, claimedDeadline))
                return true;
        }
    }

    private void RearmEscalationAfterFailedPersist(string ipAddress, long now, long claimedDeadline)
    {
        _nextEscalationTimestamp.TryUpdate(ipAddress, now + PersistRetryIntervalTicks, claimedDeadline);
    }

    private void PruneStaleTrackingState(long now)
    {
        var due = Interlocked.Read(ref _nextPruneTimestamp);

        if (now < due || Interlocked.CompareExchange(ref _nextPruneTimestamp, now + PruneIntervalTicks, due) != due)
            return;

        foreach (var entry in _violationWindows)
            if (now - entry.Value.WindowStartTimestamp > ViolationWindowTicks)
                _violationWindows.TryRemove(entry);

        foreach (var entry in _nextEscalationTimestamp)
            if (now >= entry.Value)
                _nextEscalationTimestamp.TryRemove(entry);
    }

    private async ValueTask EscalateAsync(string ipAddress, CancellationToken ct)
    {
        switch (await BlockAndKickAsync(ipAddress, ct).ConfigureAwait(false))
        {
            case EscalationOutcome.Blocked:
                _escalatedIps[ipAddress] = 0;
                break;
            case EscalationOutcome.SuppressedByAllowlist:
            case EscalationOutcome.PersistFailed:
                _escalatedIps.TryRemove(ipAddress, out _);
                break;
        }
    }

    private async ValueTask<EscalationOutcome> BlockAndKickAsync(string ipAddress, CancellationToken ct)
    {
        var now = _timestampProvider();

        if (!TryClaimEscalation(ipAddress, now, out var claimedDeadline))
            return EscalationOutcome.AlreadyEscalated;

        PruneStaleTrackingState(now);

        if (await IsOperatorAllowlistedAsync(ipAddress, ct).ConfigureAwait(false))
        {
            logger?.IpBlockSkippedForOperatorAllowlist(ipAddress);
            return EscalationOutcome.SuppressedByAllowlist;
        }

        var kicked = kickSink.KickByRemoteAddress(ipAddress);

        try
        {
            await blockIpAsync(ipAddress, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RearmEscalationAfterFailedPersist(ipAddress, _timestampProvider(), claimedDeadline);
            logger?.IpBlockPersistFailed(ex, ipAddress, kicked);
            return EscalationOutcome.PersistFailed;
        }

        logger?.IpBlocked(ipAddress, kicked);
        return EscalationOutcome.Blocked;
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

    private readonly record struct ViolationWindow(long WindowStartTimestamp, int Count);

    private enum EscalationOutcome
    {
        Blocked,
        AlreadyEscalated,
        SuppressedByAllowlist,
        PersistFailed
    }
}

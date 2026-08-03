using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Fenrir.Security.RateLimiting;

public sealed class LoginIpRateLimiter
{
    private const int Capacity = 5;
    private const double TokensPerSecond = 1d / 10d;

    private const int PurgeIntervalCalls = 500;

    private const int MaxTrackedFailureKeys = 50_000;

    private static readonly long IdleTicksBeforePurge =
        (long)(TimeSpan.FromMinutes(10).TotalSeconds * Stopwatch.Frequency);

    private static readonly long FailureWindowTicks =
        (long)(TimeSpan.FromMinutes(15).TotalSeconds * Stopwatch.Frequency);

    private readonly ConcurrentDictionary<string, Entry> _buckets = new();

    private readonly ConcurrentDictionary<SourceLoginKey, FailureWindow> _failures = new();

    private long _callCounter;

    public bool TryConsume(IPEndPoint? remoteEndPoint)
    {
        if (remoteEndPoint is null)
            return true;

        if (Interlocked.Increment(ref _callCounter) % PurgeIntervalCalls == 0)
            PurgeStaleEntries();

        var key = NormalizeSource(remoteEndPoint.Address);
        var entry = _buckets.GetOrAdd(key, static _ => new Entry(new TokenBucket(Capacity, TokensPerSecond)));
        entry.Touch();

        return entry.Bucket.TryConsume();
    }

    public int RecentFailureCount(IPEndPoint? remoteEndPoint, string loginName)
    {
        return TryBuildKey(remoteEndPoint, loginName, out var key) && _failures.TryGetValue(key, out var window)
            ? window.CountWithinWindow(FailureWindowTicks)
            : 0;
    }

    public void RecordFailure(IPEndPoint? remoteEndPoint, string loginName)
    {
        if (!TryBuildKey(remoteEndPoint, loginName, out var key))
            return;

        if (_failures.TryGetValue(key, out var existing))
        {
            existing.Increment(FailureWindowTicks);
            return;
        }

        if (_failures.Count >= MaxTrackedFailureKeys)
        {
            PurgeStaleFailures();

            if (_failures.Count >= MaxTrackedFailureKeys)
                return;
        }

        _failures.GetOrAdd(key, static _ => new FailureWindow()).Increment(FailureWindowTicks);
    }

    public void ClearFailures(IPEndPoint? remoteEndPoint, string loginName)
    {
        if (TryBuildKey(remoteEndPoint, loginName, out var key))
            _failures.TryRemove(key, out _);
    }

    public bool TryClaimThrottleReport(IPEndPoint? remoteEndPoint, string loginName)
    {
        return TryBuildKey(remoteEndPoint, loginName, out var key) && _failures.TryGetValue(key, out var window) &&
               window.TryClaimReport(FailureWindowTicks);
    }

    // SQL Server ignores trailing whitespace when matching NVARCHAR, so the key must too or padding forks the bucket.
    private static bool TryBuildKey(IPEndPoint? remoteEndPoint, string loginName, out SourceLoginKey key)
    {
        if (remoteEndPoint is null || string.IsNullOrWhiteSpace(loginName))
        {
            key = default;
            return false;
        }

        key = new SourceLoginKey(NormalizeSource(remoteEndPoint.Address), loginName.TrimEnd());
        return true;
    }

    private static string NormalizeSource(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        Span<byte> octets = stackalloc byte[16];

        if (!address.TryWriteBytes(octets, out var written) || written != octets.Length)
            return address.ToString();

        octets[8..].Clear();
        return new IPAddress(octets).ToString();
    }

    private void PurgeStaleEntries()
    {
        var now = Stopwatch.GetTimestamp();

        foreach (var (key, entry) in _buckets)
            if (now - Interlocked.Read(ref entry.LastAccessTimestamp) > IdleTicksBeforePurge)
                _buckets.TryRemove(key, out _);

        PurgeStaleFailures();
    }

    private void PurgeStaleFailures()
    {
        foreach (var (key, window) in _failures)
            if (window.IsExpired(FailureWindowTicks))
                _failures.TryRemove(key, out _);
    }

    private readonly record struct SourceLoginKey(string Source, string LoginName)
    {
        public bool Equals(SourceLoginKey other)
        {
            return string.Equals(Source, other.Source, StringComparison.Ordinal) &&
                   string.Equals(LoginName, other.LoginName, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Source, LoginName.GetHashCode(StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class Entry(TokenBucket bucket)
    {
        public readonly TokenBucket Bucket = bucket;
        public long LastAccessTimestamp = Stopwatch.GetTimestamp();

        public void Touch()
        {
            Interlocked.Exchange(ref LastAccessTimestamp, Stopwatch.GetTimestamp());
        }
    }

    private sealed class FailureWindow
    {
        private readonly Lock _gate = new();
        private int _count;
        private bool _reported;
        private long _windowStartTimestamp = Stopwatch.GetTimestamp();

        public void Increment(long windowTicks)
        {
            var now = Stopwatch.GetTimestamp();

            lock (_gate)
            {
                if (now - _windowStartTimestamp > windowTicks)
                {
                    _windowStartTimestamp = now;
                    _count = 0;
                    _reported = false;
                }

                if (_count < int.MaxValue)
                    _count++;
            }
        }

        public int CountWithinWindow(long windowTicks)
        {
            lock (_gate)
                return Stopwatch.GetTimestamp() - _windowStartTimestamp > windowTicks ? 0 : _count;
        }

        public bool TryClaimReport(long windowTicks)
        {
            lock (_gate)
            {
                if (_reported || Stopwatch.GetTimestamp() - _windowStartTimestamp > windowTicks)
                    return false;

                _reported = true;
                return true;
            }
        }

        public bool IsExpired(long windowTicks)
        {
            lock (_gate)
                return Stopwatch.GetTimestamp() - _windowStartTimestamp > windowTicks;
        }
    }
}

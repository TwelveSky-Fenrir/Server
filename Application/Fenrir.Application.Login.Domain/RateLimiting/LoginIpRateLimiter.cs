using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Fenrir.Network.Dispatch.RateLimiting;

namespace Fenrir.Application.Login.Domain.RateLimiting;

public sealed class LoginIpRateLimiter
{
    private const int Capacity = 5;
    private const double TokensPerSecond = 1d / 10d;

    private const int PurgeIntervalCalls = 500;

    private static readonly long IdleTicksBeforePurge =
        (long)(TimeSpan.FromMinutes(10).TotalSeconds * Stopwatch.Frequency);

    private readonly ConcurrentDictionary<string, Entry> _buckets = new();
    private long _callCounter;

    public bool TryConsume(IPEndPoint? remoteEndPoint)
    {
        if (remoteEndPoint is null)
            return true;

        if (Interlocked.Increment(ref _callCounter) % PurgeIntervalCalls == 0)
            PurgeStaleEntries();

        var key = remoteEndPoint.Address.ToString();
        var entry = _buckets.GetOrAdd(key, static _ => new Entry(new TokenBucket(Capacity, TokensPerSecond)));
        entry.Touch();

        return entry.Bucket.TryConsume();
    }

    private void PurgeStaleEntries()
    {
        var now = Stopwatch.GetTimestamp();

        foreach (var (key, entry) in _buckets)
            if (now - Interlocked.Read(ref entry.LastAccessTimestamp) > IdleTicksBeforePurge)
                _buckets.TryRemove(key, out _);
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
}

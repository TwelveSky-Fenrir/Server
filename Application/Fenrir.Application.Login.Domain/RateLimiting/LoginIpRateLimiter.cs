using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Fenrir.Network.Dispatch.RateLimiting;

namespace Fenrir.Application.Login.Domain.RateLimiting;

/// <summary>Per-IP bucket for CL_LOGIN_SEND; catches bruteforce that reconnects to dodge the per-session bucket.</summary>
public sealed class LoginIpRateLimiter
{
    // Looser than the per-session Auth policy (3/5s): one IP can host several legit sessions (NAT/shared connection).
    private const int Capacity = 5;
    private const double TokensPerSecond = 1d / 10d;

    // Opportunistic purge (every Nth call) instead of a background timer, to bound dictionary size cheaply.
    private const int PurgeIntervalCalls = 500;

    private static readonly long IdleTicksBeforePurge =
        (long)(TimeSpan.FromMinutes(10).TotalSeconds * Stopwatch.Frequency);

    private readonly ConcurrentDictionary<string, Entry> _buckets = new();
    private long _callCounter;

    /// <summary>Fail-open when no IP is known (unit tests, non-TCP transport): this is defense-in-depth, not the sole guard.</summary>
    public bool TryConsume(IPEndPoint? remoteEndPoint)
    {
        if (remoteEndPoint is null)
            return true;

        if (Interlocked.Increment(ref _callCounter) % PurgeIntervalCalls == 0)
            PurgeStaleEntries();

        // Keyed on address only: the port changes across reconnects, the address doesn't.
        var key = remoteEndPoint.Address.ToString();
        var entry = _buckets.GetOrAdd(key, static _ => new Entry(new TokenBucket(Capacity, TokensPerSecond)));
        entry.Touch();

        return entry.Bucket.TryConsume();
    }

    /// <summary>Drops idle buckets; racing a concurrent <see cref="TryConsume" /> just resets that bucket, harmlessly.</summary>
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

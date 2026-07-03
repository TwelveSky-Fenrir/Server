using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Fenrir.Network.RateLimiting;

namespace Fenrir.Application.Login.RateLimiting;

/// <summary>
///     IP-keyed anti-bruteforce gate for op11 <c>CL_LOGIN_SEND</c>. Complements (does not replace)
///     <c>Fenrir.Network.RateLimiting.SessionRateLimiter</c>: that one buckets per <c>SessionId</c>, so a
///     bruteforcer that simply reconnects TCP before every attempt gets a brand-new bucket each time and never
///     feels the limit. This bucket is keyed on the client's IP address instead, which a reconnect does not change.
/// </summary>
public sealed class LoginIpRateLimiter
{
    // Wider/slower than OpcodeRateLimiterPolicy's Auth class (3 capacity, 1 token/5s): that budget is per-session
    // and this one is per-IP, and a single IP can legitimately host several concurrent LoginClientSessions (NAT,
    // a shared connection at a cybercafe, a household behind one router). Squeezing the IP-wide budget down to
    // the session-wide one would false-positive those real players; 5 capacity / 1 token per 10s still caps a
    // reconnect-and-retry loop at a few attempts a minute, which is what this class exists to stop.
    private const int Capacity = 5;
    private const double TokensPerSecond = 1d / 10d;

    // Opportunistic purge instead of a dedicated Timer/BackgroundService: cheapest way to bound the dictionary's
    // size under an IP-scanning attacker without a second thread to start/stop alongside the server lifecycle.
    // Checked via an atomic counter rather than "on every call" so the scan itself doesn't run on every request.
    private const int PurgeIntervalCalls = 500;

    private static readonly long IdleTicksBeforePurge =
        (long)(TimeSpan.FromMinutes(10).TotalSeconds * Stopwatch.Frequency);

    private readonly ConcurrentDictionary<string, Entry> _buckets = new();
    private long _callCounter;

    /// <summary>
    ///     Fail-open on <see langword="null" />: a session whose transport was never a real accepted socket (unit
    ///     tests, or any future non-TCP transport) has no IP to key on. IP throttling is defense-in-depth on top of
    ///     the state machine and the per-session bucket, not the sole guard, so "no IP known" must not block it.
    /// </summary>
    public bool TryConsume(IPEndPoint? remoteEndPoint)
    {
        if (remoteEndPoint is null)
            return true;

        if (Interlocked.Increment(ref _callCounter) % PurgeIntervalCalls == 0)
            PurgeStaleEntries();

        // Port only, not the address, changes across reconnects, so the address alone must be the key.
        var key = remoteEndPoint.Address.ToString();
        var entry = _buckets.GetOrAdd(key, static _ => new Entry(new TokenBucket(Capacity, TokensPerSecond)));
        entry.Touch();

        return entry.Bucket.TryConsume();
    }

    /// <summary>
    ///     Drops buckets idle for longer than <see cref="IdleTicksBeforePurge" />. Benign race with a concurrent
    ///     <see cref="TryConsume" /> for the same key: at worst a bucket gets removed right after being touched and
    ///     the next attempt starts a fresh, fully-topped-up one — a rare, harmless reset, not a leak or a bypass
    ///     worth synchronizing against (an attacker would need to land in exactly that window every single time).
    /// </summary>
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

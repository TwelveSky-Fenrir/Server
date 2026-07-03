using System.Diagnostics;

namespace Fenrir.Network.RateLimiting;

/// <summary>
///     Classic token bucket: <see cref="Capacity" /> tokens max, refilled continuously at
///     <see cref="TokensPerSecond" />. Refill is computed lazily from elapsed <see cref="Stopwatch" /> ticks inside
///     <see cref="TryConsume" /> rather than via a background timer, so a bucket nobody touches costs nothing.
/// </summary>
public sealed class TokenBucket
{
    private readonly Lock _gate = new();
    private long _lastRefillTimestamp;
    private double _tokens;

    public TokenBucket(int capacity, double tokensPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegative(tokensPerSecond);

        Capacity = capacity;
        TokensPerSecond = tokensPerSecond;
        _tokens = capacity; // starts full: a session's first packet of a class must not be penalized for buckets it never touched before
        _lastRefillTimestamp = Stopwatch.GetTimestamp();
    }

    public int Capacity { get; }

    public double TokensPerSecond { get; }

    /// <summary>
    ///     Refills from elapsed wall-clock time, then withdraws <paramref name="count" /> tokens if enough are
    ///     available. The refill computation and the withdrawal must observe one consistent
    ///     (tokens, lastRefillTimestamp) snapshot, so both happen under <see cref="_gate" /> rather than via separate
    ///     Interlocked ops on each field (which could race: thread A refills using a timestamp thread B is about to
    ///     overwrite, double-crediting tokens). Contention is expected to be near-zero — a session's own read loop is
    ///     normally the sole caller for its buckets — so a lightweight <see cref="Lock" /> (same primitive already
    ///     used by <see cref="Sessions.ClientSession" /> for its send path) beats a hand-rolled CAS retry loop on
    ///     both simplicity and correctness risk, for no measurable throughput cost.
    /// </summary>
    public bool TryConsume(int count = 1)
    {
        var now = Stopwatch.GetTimestamp();

        lock (_gate)
        {
            var elapsedSeconds = (now - _lastRefillTimestamp) / (double)Stopwatch.Frequency;
            if (elapsedSeconds > 0)
            {
                _tokens = Math.Min(Capacity, _tokens + elapsedSeconds * TokensPerSecond);
                _lastRefillTimestamp = now;
            }

            if (_tokens < count)
                return false;

            _tokens -= count;
            return true;
        }
    }
}

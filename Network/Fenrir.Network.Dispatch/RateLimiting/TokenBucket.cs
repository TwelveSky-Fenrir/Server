using System.Diagnostics;

namespace Fenrir.Network.Dispatch.RateLimiting;

// Token bucket refilled lazily from elapsed Stopwatch ticks in TryConsume, not a timer, so an idle bucket costs nothing.
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
        _tokens = capacity; // starts full so a session's first packet of a class isn't penalized
        _lastRefillTimestamp = Stopwatch.GetTimestamp();
    }

    public int Capacity { get; }

    public double TokensPerSecond { get; }

    // Refill and withdrawal share one lock so (tokens, lastRefillTimestamp) stays a consistent snapshot;
    // separate Interlocked ops on each field could race and double-credit tokens.
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

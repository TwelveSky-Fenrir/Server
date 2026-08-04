using System.Diagnostics;

namespace Fenrir.Security.RateLimiting;

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
        _tokens = capacity;
        _lastRefillTimestamp = Stopwatch.GetTimestamp();
    }

    public int Capacity { get; }

    public double TokensPerSecond { get; }

    public bool TryConsume(int count = 1)
    {
        var now = Stopwatch.GetTimestamp();

        lock (_gate)
        {
            Refill(now);

            if (_tokens < count)
                return false;

            _tokens -= count;
            return true;
        }
    }

    public void Refund(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var now = Stopwatch.GetTimestamp();

        lock (_gate)
        {
            Refill(now);
            _tokens = Math.Min(Capacity, _tokens + count);
        }
    }

    private void Refill(long now)
    {
        var elapsedSeconds = (now - _lastRefillTimestamp) / (double)Stopwatch.Frequency;

        if (elapsedSeconds <= 0)
            return;

        _tokens = Math.Min(Capacity, _tokens + elapsedSeconds * TokensPerSecond);
        _lastRefillTimestamp = now;
    }
}

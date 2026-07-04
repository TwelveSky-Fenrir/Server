using Fenrir.Network.RateLimiting;

namespace Fenrir.Network.Tests.RateLimiting;

public class TokenBucketTests
{
    [Fact]
    public void TryConsume_ExactlyCapacityTimes_AllSucceedThenNextFails()
    {
        // TokensPerSecond = 0 removes refill entirely, so the (capacity+1)-th call fails deterministically.
        var bucket = new TokenBucket(5, 0d);

        for (var i = 0; i < 5; i++)
            Assert.True(bucket.TryConsume(), $"Consume #{i + 1} of {5} should succeed while the bucket starts full.");

        Assert.False(bucket.TryConsume());
    }

    [Fact]
    public void TryConsume_AfterRealDelay_RefillsAtLeastOneToken()
    {
        // A high TokensPerSecond means even a short sleep refills the small capacity, avoiding a flaky long sleep.
        var bucket = new TokenBucket(1, 1_000d);

        Assert.True(bucket.TryConsume());
        Assert.False(bucket.TryConsume());

        Thread.Sleep(50);

        Assert.True(bucket.TryConsume());
    }

    [Fact]
    public void TryConsume_RefillNeverExceedsCapacity()
    {
        // Refill is clamped to Capacity -- an idle bucket must not accumulate unbounded burst credit.
        var bucket = new TokenBucket(2, 1_000d);

        Thread.Sleep(50);

        Assert.True(bucket.TryConsume());
        Assert.True(bucket.TryConsume());
        Assert.False(bucket.TryConsume());
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucket(0, 1d));
    }

    [Fact]
    public void Constructor_RejectsNegativeTokensPerSecond()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucket(1, -1d));
    }
}

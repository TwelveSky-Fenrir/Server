using System.Net;
using Fenrir.Application.Login.Domain.RateLimiting;

namespace Fenrir.Application.Login.Tests.RateLimiting;

public class LoginIpRateLimiterTests
{
    // Must track LoginIpRateLimiter's private Capacity (5), which the class does not expose.
    private const int ExpectedCapacity = 5;

    [Fact]
    public void TryConsume_SameIp_ExhaustsBudgetThenNextAttemptFails()
    {
        var limiter = new LoginIpRateLimiter();
        var attacker = new IPEndPoint(IPAddress.Parse("203.0.113.10"), 40000);

        for (var i = 0; i < ExpectedCapacity; i++)
            Assert.True(limiter.TryConsume(attacker),
                $"Attempt #{i + 1} of {ExpectedCapacity} should still be within budget.");

        Assert.False(limiter.TryConsume(attacker));
    }

    [Fact]
    public void TryConsume_SameIpDifferentPort_SharesTheSameBudget()
    {
        // Port must NOT be part of the key, or a bruteforcer reconnecting each time would never be caught.
        var limiter = new LoginIpRateLimiter();
        var address = IPAddress.Parse("203.0.113.10");

        for (var i = 0; i < ExpectedCapacity; i++)
            Assert.True(limiter.TryConsume(new IPEndPoint(address, 40000 + i)));

        Assert.False(limiter.TryConsume(new IPEndPoint(address, 59999)));
    }

    [Fact]
    public void TryConsume_DifferentIp_HasAnIndependentBudget()
    {
        var limiter = new LoginIpRateLimiter();
        var attacker = new IPEndPoint(IPAddress.Parse("203.0.113.10"), 40000);
        var otherPlayer = new IPEndPoint(IPAddress.Parse("198.51.100.20"), 40000);

        for (var i = 0; i < ExpectedCapacity; i++)
            Assert.True(limiter.TryConsume(attacker));
        Assert.False(limiter.TryConsume(attacker));

        Assert.True(limiter.TryConsume(otherPlayer));
    }

    [Fact]
    public void TryConsume_NullEndPoint_AlwaysSucceeds()
    {
        // No IP to throttle on (e.g. an in-memory transport) must fail open, not block the caller.
        var limiter = new LoginIpRateLimiter();

        for (var i = 0; i < ExpectedCapacity + 5; i++)
            Assert.True(limiter.TryConsume(null));
    }
}

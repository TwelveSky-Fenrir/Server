using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.RateLimiting;

namespace Fenrir.Network.Tests.RateLimiting;

public class SessionRateLimiterTests
{
    private const FenrirServer Server = FenrirServer.Zone;
    private const byte Opcode = Opcodes.Zone.Incoming.Heartbeat;

    [Fact]
    public void TryConsume_TwoSessions_HaveIndependentBuckets()
    {
        var limiter = new SessionRateLimiter();
        var (capacity, _) = OpcodeRateLimiterPolicy.PolicyFor(Server, Opcode);

        const long sessionA = 1;
        const long sessionB = 2;

        for (var i = 0; i < capacity; i++)
            Assert.True(limiter.TryConsume(sessionA, Server, Opcode));

        Assert.False(limiter.TryConsume(sessionA, Server, Opcode));

        Assert.True(limiter.TryConsume(sessionB, Server, Opcode));
    }

    [Fact]
    public void Remove_ThenTryConsume_StartsFromAFreshBucket()
    {
        var limiter = new SessionRateLimiter();
        var (capacity, _) = OpcodeRateLimiterPolicy.PolicyFor(Server, Opcode);

        const long sessionId = 42;

        for (var i = 0; i < capacity; i++)
            Assert.True(limiter.TryConsume(sessionId, Server, Opcode));

        Assert.False(limiter.TryConsume(sessionId, Server, Opcode));

        limiter.Remove(sessionId);

        Assert.True(limiter.TryConsume(sessionId, Server, Opcode));
    }

    [Fact]
    public void Remove_UnknownSessionId_DoesNotThrow()
    {
        var limiter = new SessionRateLimiter();

        var exception = Record.Exception(() => limiter.Remove(999));

        Assert.Null(exception);
    }
}

using Fenrir.Contracts;
using Fenrir.Contracts.Wire;
using Fenrir.Network.RateLimiting;

namespace Fenrir.Network.Tests.RateLimiting;

public class SessionRateLimiterTests
{
    // Heartbeat's tiny capacity (see OpcodeRateLimiterPolicy) makes exhaustion cheap to reach, and its slow
    // refill rate keeps the whole test well within one token's worth of wall-clock time, so nothing here can
    // flake on timing. Capacity is read from the policy rather than hard-coded so the test survives re-tuning.
    private const FenrirServer Server = FenrirServer.Zone;
    private const byte Opcode = Opcodes.Zone.Incoming.HeartbeatSend;

    [Fact]
    public void TryConsume_TwoSessions_HaveIndependentBuckets()
    {
        var limiter = new SessionRateLimiter();
        var (capacity, _) = OpcodeRateLimiterPolicy.PolicyFor(Server, Opcode);

        const long sessionA = 1;
        const long sessionB = 2;

        for (var i = 0; i < capacity; i++)
            Assert.True(limiter.TryConsume(sessionA, Server, Opcode));

        // Session A's bucket for this (server, opcode) is now empty.
        Assert.False(limiter.TryConsume(sessionA, Server, Opcode));

        // Session B never touched this pair before -> its own bucket must start full, unaffected by A's
        // exhaustion, proving the two sessions don't share state.
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

        // Black-box evidence that Remove purged the exhausted bucket: the very next TryConsume for the same
        // session+opcode gets a brand-new, fully-topped-up bucket rather than reusing the depleted one.
        Assert.True(limiter.TryConsume(sessionId, Server, Opcode));
    }

    [Fact]
    public void Remove_UnknownSessionId_DoesNotThrow()
    {
        var limiter = new SessionRateLimiter();

        // SessionRateLimiter's backing dictionary is private, so whether Remove leaves no trace for a session
        // that was never added isn't otherwise observable from outside the type -- this only pins down that
        // calling it (e.g. on a double-disconnect race) is safe.
        var exception = Record.Exception(() => limiter.Remove(999));

        Assert.Null(exception);
    }
}

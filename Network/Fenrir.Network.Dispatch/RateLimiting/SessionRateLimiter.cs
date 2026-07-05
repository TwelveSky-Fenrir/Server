using System.Collections.Concurrent;
using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Dispatch.RateLimiting;

// Per-session table of TokenBuckets, created lazily per (server, opcode) actually hit rather than pre-populated.
public sealed class SessionRateLimiter : ISessionRateLimiter
{
    // Nested so Remove(sessionId) drops one entry instead of scanning the whole table.
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<(FenrirServer Server, byte Opcode), TokenBucket>>
        _buckets = new();

    public bool TryConsume(long sessionId, FenrirServer server, byte opcode)
    {
        var sessionBuckets = _buckets.GetOrAdd(sessionId,
            static _ => new ConcurrentDictionary<(FenrirServer Server, byte Opcode), TokenBucket>());

        var bucket = sessionBuckets.GetOrAdd((server, opcode), static key =>
        {
            var policy = OpcodeRateLimiterPolicy.PolicyFor(key.Server, key.Opcode);
            return new TokenBucket(policy.Capacity, policy.TokensPerSecond);
        });

        return bucket.TryConsume();
    }

    // Benign race with a concurrent TryConsume: a fresh bucket may get created right after removal but is never touched again.
    public void Remove(long sessionId)
    {
        _buckets.TryRemove(sessionId, out _);
    }
}

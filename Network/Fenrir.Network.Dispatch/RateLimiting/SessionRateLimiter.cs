using System.Collections.Concurrent;
using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Dispatch.RateLimiting;

public sealed class SessionRateLimiter : ISessionRateLimiter
{
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

    public void Remove(long sessionId)
    {
        _buckets.TryRemove(sessionId, out _);
    }
}

using System.Collections.Concurrent;
using Fenrir.Core.Abstractions;
using Fenrir.Core.Wire;

namespace Fenrir.Security.RateLimiting;

public sealed class SessionRateLimiter : ISessionRateLimiter
{
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<(FenrirServer Server, byte Opcode), TokenBucket>>
        _buckets = new();

    private readonly ConcurrentDictionary<long, TokenBucket> _gmCommandBuckets = new();

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

    public bool TryConsumeGmCommand(long sessionId)
    {
        var bucket = _gmCommandBuckets.GetOrAdd(sessionId, static _ => new TokenBucket(
            OpcodeRateLimiterPolicy.GmCommand.Capacity, OpcodeRateLimiterPolicy.GmCommand.TokensPerSecond));

        return bucket.TryConsume();
    }

    public void Remove(long sessionId)
    {
        _buckets.TryRemove(sessionId, out _);
        _gmCommandBuckets.TryRemove(sessionId, out _);
    }
}

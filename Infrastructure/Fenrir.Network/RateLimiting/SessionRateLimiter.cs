using System.Collections.Concurrent;
using Fenrir.Contracts.Wire;

namespace Fenrir.Network.RateLimiting;

/// <summary>
///     Per-session table of <see cref="TokenBucket" />s, one per (server, opcode) pair the session has actually hit
///     — buckets are created lazily via <see cref="OpcodeRateLimiterPolicy.PolicyFor" /> rather than pre-populated
///     for every declared opcode, since a given session typically exercises only a handful of them.
/// </summary>
public sealed class SessionRateLimiter : ISessionRateLimiter
{
    // Nested rather than a single dictionary keyed on (sessionId, server, opcode): Remove(sessionId) then drops one
    // entry instead of scanning/filtering the whole table, which matters once thousands of sessions have churned.
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

    /// <summary>
    ///     Benign race with a concurrent <see cref="TryConsume" /> for the same (already-disconnecting) session: at
    ///     worst a fresh, fully-topped-up bucket gets created right after removal and is then never looked at again
    ///     — no leak, since the session is torn down either way and nothing re-adds it after this call.
    /// </summary>
    public void Remove(long sessionId)
    {
        _buckets.TryRemove(sessionId, out _);
    }
}

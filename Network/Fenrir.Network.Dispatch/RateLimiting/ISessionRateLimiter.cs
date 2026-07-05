using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Dispatch.RateLimiting;

// Per-session, per-opcode-class buckets; keyed on opcode since the legacy wire has no sequence number.
public interface ISessionRateLimiter
{
    // false = bucket empty; caller decides the response. SessionLoop, today's sole caller, treats it as an
    // immediate disconnect rather than per-class throttle-vs-disconnect distinction.
    public bool TryConsume(long sessionId, FenrirServer server, byte opcode);

    /// <summary>Drops all buckets for a session, to avoid an unbounded dictionary.</summary>
    public void Remove(long sessionId);
}

using Fenrir.Contracts.Wire;

namespace Fenrir.Network.RateLimiting;

/// <summary>
///     Per-session, per-opcode-class token buckets (architecture reference §8.5, adapted to legacy opcodes since
///     there is no wire sequence number to anchor anti-replay on).
/// </summary>
public interface ISessionRateLimiter
{
    /// <summary>
    ///     <c>false</c> means the bucket for this session+opcode-class is empty right now. Today's sole caller
    ///     (<see cref="Dispatching.SessionLoop" />) treats any <c>false</c> as an immediate disconnect, matching its
    ///     own "any violation closes the socket" policy — §8.5's per-class throttle-vs-disconnect nuance (e.g.
    ///     silently dropping an over-budget Movement packet instead of dropping the connection) is not implemented
    ///     by this interface itself; a caller wanting that distinction must derive it from <paramref name="opcode" />
    ///     itself before deciding how to react to a <c>false</c>.
    /// </summary>
    public bool TryConsume(long sessionId, FenrirServer server, byte opcode);

    /// <summary>Drops all buckets for a session — call on disconnect to avoid an unbounded dictionary.</summary>
    public void Remove(long sessionId);
}

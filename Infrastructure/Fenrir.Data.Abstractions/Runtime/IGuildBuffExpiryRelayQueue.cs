namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Non-blocking producer handle for the cross-shard guild-buff-expiry relay outbox --
///     <c>Hosting.Guilds.GuildBuffDecayHost</c> only needs this narrow surface, not the concrete
///     <c>GuildBuffExpiryRelayHost</c>'s own lifecycle. Same "non-generic handle for a hot call site" shape as
///     <see cref="IGuildTribeBroadcastRelayQueue" /> -- see that interface's own remarks for why the producer
///     depends on <c>Fenrir.Data.Abstractions</c> only rather than the concrete Hosting-layer type directly.
/// </summary>
public interface IGuildBuffExpiryRelayQueue
{
    /// <summary>
    ///     Non-blocking, synchronous enqueue. Never awaits, never throws on backpressure. Returns false only if
    ///     the bounded channel is full; the cross-shard fan-out for this one exhaustion push is then silently
    ///     dropped -- the SAME-shard delivery the caller already performed synchronously, before calling this,
    ///     is entirely unaffected either way.
    /// </summary>
    public bool Enqueue(GuildBuffExpiryRelayEntry entry);
}

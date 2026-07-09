namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Non-blocking producer handle for the cross-shard RvR/siege world-event relay outbox --
///     <c>ZoneCenterBroadcastIngestor</c>/<c>ZoneEventBroadcaster</c> only need this narrow surface, not the
///     concrete <c>RvrSiegeEventRelayHost</c>'s own lifecycle. Same "non-generic handle for a hot synchronous
///     call site" shape as <see cref="IGuildTribeBroadcastRelayQueue" />/<see cref="IProxyShopExpirationRelayQueue" />
///     -- see either interface's own remarks for why <c>*.Domain</c>/<c>*.Hosting</c> depends on
///     <c>Fenrir.Data.Abstractions</c> only rather than the concrete Hosting project directly.
/// </summary>
public interface IRvrSiegeEventRelayQueue
{
    /// <summary>
    ///     Non-blocking, synchronous enqueue. Never awaits, never throws on backpressure. Returns false only if
    ///     the bounded channel is full; the cross-shard fan-out for this one world event is then silently
    ///     dropped -- the SAME-shard mutation/broadcast the caller already performed synchronously, before
    ///     calling this, is entirely unaffected either way.
    /// </summary>
    public bool Enqueue(RvrSiegeEventRelayEntry entry);
}

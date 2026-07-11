namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Non-blocking producer handle for the cross-shard party-resync relay outbox -- the future producer (a
///     party-resync trigger on a reconnecting member's first zone entry, once a persisted party-name anchor
///     exists to gate on) only needs this narrow surface, not the concrete <c>PartyResyncRelayHost</c>'s own
///     lifecycle. Same "non-generic handle for a call site that must not await" shape as
///     <see cref="IGuildTribeBroadcastRelayQueue" />/<see cref="ISocialCrossShardRelayQueue" /> -- see those
///     interfaces' own remarks for why <c>*.Services</c> depends on <c>Fenrir.Data.Abstractions</c> only rather
///     than the concrete Hosting project directly.
/// </summary>
public interface IPartyResyncRelayQueue
{
    /// <summary>
    ///     Non-blocking, synchronous enqueue of one cross-shard party-resync row. Never awaits, never throws on
    ///     backpressure. Returns false only if the bounded channel is full; the cross-shard leg of this one
    ///     resync is then silently dropped -- a missed resync manifests only as the reconnecting client's party
    ///     UI staying/going empty, never as duped or lost durable state (no party membership is persisted on
    ///     this path).
    /// </summary>
    public bool Enqueue(PartyResyncRelayEntry entry);
}

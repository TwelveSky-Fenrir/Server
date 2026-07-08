namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Non-blocking producer handle for the cross-shard guild/tribe broadcast relay outbox -- an
///     <c>IInlinePacketHandler</c>-backed call site (<c>GuildAnnouncementService</c>/<c>GuildChatService</c>/
///     <c>TribeAnnouncementService</c>/<c>TribeAnnouncementScrollService</c>) only needs this narrow surface,
///     not the concrete <c>GuildTribeBroadcastRelayHost</c>'s own lifecycle. Same "non-generic handle for a
///     hot synchronous call site" shape as <c>IEventLogQueue</c> -- see that interface's own remarks for why
///     <c>*.Services</c> depends on <c>Fenrir.Data.Abstractions</c> only rather than the WriteBehind project
///     directly.
/// </summary>
public interface IGuildTribeBroadcastRelayQueue
{
    /// <summary>
    ///     Non-blocking, synchronous enqueue. Never awaits, never throws on backpressure. Returns false only
    ///     if the bounded channel is full; the cross-shard fan-out for this one broadcast is then silently
    ///     dropped -- the SAME-shard delivery the caller already performed synchronously, before calling this,
    ///     is entirely unaffected either way.
    /// </summary>
    public bool Enqueue(GuildTribeBroadcastRelayEntry entry);
}

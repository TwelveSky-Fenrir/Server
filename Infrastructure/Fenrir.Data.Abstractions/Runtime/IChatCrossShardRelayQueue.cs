namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Non-blocking producer handle for the cross-shard whisper relay outbox -- <c>WhisperService.ResolveAsync</c>
///     only needs this narrow surface (enqueue one whisper after the cross-shard directory resolves the target
///     on another shard), not the concrete <c>ChatCrossShardRelayHost</c>'s own lifecycle. Same "non-generic
///     handle for a call site that must not await" shape as <see cref="ISocialCrossShardRelayQueue" /> -- see
///     that interface's own remarks for why <c>*.Services</c> depends on <c>Fenrir.Data.Abstractions</c> only
///     rather than the concrete Hosting project directly.
/// </summary>
public interface IChatCrossShardRelayQueue
{
    /// <summary>
    ///     Non-blocking, synchronous enqueue of one cross-shard whisper, after <c>WhisperService</c> has
    ///     resolved the target on another live shard. Never awaits, never throws on backpressure. Returns false
    ///     only if the bounded channel is full; the cross-shard leg of this one whisper is then silently
    ///     dropped (the sender has already been told the target was located -- see the op39 contract's
    ///     result-0-before-relay ordering).
    /// </summary>
    public bool Enqueue(ChatCrossShardWhisperEntry entry);
}

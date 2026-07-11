using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     CaeriusNet wrapper around <c>runtime.ChatCrossShardRelay</c>/<c>runtime.ChatCrossShardRelayCursor</c>
///     -- the cross-shard delivery mechanism for the whisper (op39 / <c>SECRET_CHAT</c>) point-to-point chat
///     flow (see <see cref="ChatCrossShardWhisperEntry" />'s own remarks for the full design). Point-to-point,
///     not fan-out, exactly like its sibling <see cref="ISocialCrossShardRelayRepository" /> -- <see cref="PollAsync" />
///     only ever returns rows addressed to the calling shard. Intended to be consumed exclusively by
///     <c>ChatCrossShardRelayHost</c>'s own drain/poll loop -- never called directly from an
///     <c>IAsyncPacketHandler</c>'s per-connection path.
/// </summary>
public interface IChatCrossShardRelayRepository
{
    /// <summary>
    ///     <c>usp_ChatCrossShardRelay_Publish</c>: appends one whisper row for
    ///     <see cref="ChatCrossShardWhisperEntry.TargetShardId" />'s own next poll to pick up.
    /// </summary>
    public ValueTask PublishAsync(ChatCrossShardWhisperEntry entry, CancellationToken ct);

    /// <summary>
    ///     <c>usp_ChatCrossShardRelay_Poll</c>: returns every whisper row addressed to <paramref name="shardId" />
    ///     since that shard's own last poll, advances that shard's own cursor past them, and (as a side effect)
    ///     reaps rows older than <paramref name="retentionSeconds" /> regardless of which shard they were
    ///     addressed to.
    /// </summary>
    public ValueTask<ImmutableArray<ChatCrossShardWhisperDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}

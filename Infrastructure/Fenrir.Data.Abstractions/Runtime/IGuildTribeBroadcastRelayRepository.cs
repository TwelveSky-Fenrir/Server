using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     CaeriusNet wrapper around <c>runtime.GuildTribeBroadcastRelay</c>/<c>runtime.GuildTribeBroadcastCursor</c>
///     -- the cross-shard fan-out mechanism for GuildAnnouncement/GuildChat/TribeAnnouncement/
///     TribeAnnouncementScroll (see <see cref="GuildTribeBroadcastRelayEntry" />'s own remarks for the full
///     design). Consumed exclusively by <c>GuildTribeBroadcastRelayHost</c>'s own drain/poll loop -- never
///     called directly from an <c>IInlinePacketHandler</c>'s synchronous path (see
///     <see cref="IGuildTribeBroadcastRelayQueue" /> for that boundary).
/// </summary>
public interface IGuildTribeBroadcastRelayRepository
{
    /// <summary>
    ///     <c>usp_GuildTribeBroadcastRelay_Publish</c>: appends one row for every OTHER live shard to pick up
    ///     on its own next poll.
    /// </summary>
    public ValueTask PublishAsync(GuildTribeBroadcastRelayEntry entry, CancellationToken ct);

    /// <summary>
    ///     <c>usp_GuildTribeBroadcastRelay_Poll</c>: returns every row published by some other shard since
    ///     this shard's own last poll, advances this shard's own cursor past them, and (as a side effect)
    ///     reaps rows older than <paramref name="retentionSeconds" /> regardless of which shard published them.
    /// </summary>
    public ValueTask<ImmutableArray<GuildTribeBroadcastRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}

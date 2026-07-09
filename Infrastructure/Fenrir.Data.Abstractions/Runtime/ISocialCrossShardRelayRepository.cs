using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     CaeriusNet wrapper around <c>runtime.SocialCrossShardRelay</c>/<c>runtime.SocialCrossShardRelayCursor</c>
///     -- the cross-shard fan-out mechanism for the six character-to-character negotiation flows (Party
///     invite, Friend ask, Mentor ask, Duel ask, Trade invite, Guild invite; see
///     <see cref="SocialCrossShardRelayEntry" />'s own remarks for the full design). Point-to-point, not
///     fan-out, unlike its sibling <see cref="IGuildTribeBroadcastRelayRepository" /> -- <see cref="PollAsync" />
///     only ever returns rows addressed to the calling shard. Intended to be consumed exclusively by a future
///     <c>SocialCrossShardRelayHost</c>'s own drain/poll loop -- never called directly from an
///     <c>IInlinePacketHandler</c>'s synchronous path.
/// </summary>
public interface ISocialCrossShardRelayRepository
{
    /// <summary>
    ///     <c>usp_SocialCrossShardRelay_Publish</c>: appends one Ask or Answer row for
    ///     <see cref="SocialCrossShardRelayEntry.TargetShardId" />'s own next poll to pick up.
    /// </summary>
    public ValueTask PublishAsync(SocialCrossShardRelayEntry entry, CancellationToken ct);

    /// <summary>
    ///     <c>usp_SocialCrossShardRelay_Poll</c>: returns every row addressed to <paramref name="shardId" />
    ///     since that shard's own last poll, advances that shard's own cursor past them, and (as a side
    ///     effect) reaps rows older than <paramref name="retentionSeconds" /> regardless of which shard they
    ///     were addressed to.
    /// </summary>
    public ValueTask<ImmutableArray<SocialCrossShardRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}

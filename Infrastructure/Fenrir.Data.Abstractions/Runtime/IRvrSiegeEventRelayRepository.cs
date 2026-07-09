using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     CaeriusNet wrapper around <c>runtime.RvrSiegeEventRelay</c>/<c>runtime.RvrSiegeEventRelayCursor</c> -- the
///     cross-shard fan-out mechanism for the Zone049 siege-zone-slot family (sub-codes 1-9) and the tribe-symbol/
///     alliance family (tSort 38/39/40/42/45/46/47) riding the legacy's op33
///     <c>ZCP_ZONE_BROADCAST_FOR_CENTER_SEND</c>/op94 <c>ZC_BROADCAST_INFO_RECV</c> pair (see
///     <see cref="RvrSiegeEventRelayEntry" />'s own remarks for the full design). Fan-out, not point-to-point,
///     same shape as <see cref="IGuildTribeBroadcastRelayRepository" />/<see cref="IProxyShopExpirationRelayRepository" />
///     -- <see cref="PollAsync" /> returns every row published by some OTHER shard, not just rows addressed to
///     one named target. Intended to be consumed exclusively by <c>RvrSiegeEventRelayHost</c>'s own drain/poll
///     loop -- never called directly from an <c>IInlinePacketHandler</c>'s synchronous path.
/// </summary>
public interface IRvrSiegeEventRelayRepository
{
    /// <summary>
    ///     <c>usp_RvrSiegeEventRelay_Publish</c>: appends one row for every OTHER live shard's own next poll to
    ///     pick up.
    /// </summary>
    public ValueTask PublishAsync(RvrSiegeEventRelayEntry entry, CancellationToken ct);

    /// <summary>
    ///     <c>usp_RvrSiegeEventRelay_Poll</c>: returns every row published by some OTHER shard
    ///     (<c>SourceShardId &lt;&gt; shardId</c>) since <paramref name="shardId" />'s own last poll, advances
    ///     that shard's own cursor past them, and (as a side effect) reaps rows older than
    ///     <paramref name="retentionSeconds" /> regardless of which shard published them.
    /// </summary>
    public ValueTask<ImmutableArray<RvrSiegeEventRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}

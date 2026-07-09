using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     CaeriusNet wrapper around <c>runtime.ProxyShopExpirationRelay</c>/<c>runtime.ProxyShopExpirationRelayCursor</c>
///     -- the cross-shard fan-out mechanism for the proxy-shop rental-extension consumables' best-effort
///     live-registry mirror update (see <see cref="ProxyShopExpirationRelayEntry" />'s own remarks for the
///     full design). Fan-out, not point-to-point, same shape as its sibling
///     <see cref="IGuildTribeBroadcastRelayRepository" /> -- <see cref="PollAsync" /> returns every row
///     published by some OTHER shard, not just rows addressed to one named target. Intended to be consumed
///     exclusively by <c>ProxyShopExpirationRelayHost</c>'s own drain/poll loop -- never called directly from
///     an <c>IInlinePacketHandler</c>'s synchronous path.
/// </summary>
public interface IProxyShopExpirationRelayRepository
{
    /// <summary>
    ///     <c>usp_ProxyShopExpirationRelay_Publish</c>: appends one row for every OTHER live shard's own next
    ///     poll to pick up.
    /// </summary>
    public ValueTask PublishAsync(ProxyShopExpirationRelayEntry entry, CancellationToken ct);

    /// <summary>
    ///     <c>usp_ProxyShopExpirationRelay_Poll</c>: returns every row published by some OTHER shard
    ///     (<c>SourceShardId &lt;&gt; shardId</c>) since <paramref name="shardId" />'s own last poll, advances
    ///     that shard's own cursor past them, and (as a side effect) reaps rows older than
    ///     <paramref name="retentionSeconds" /> regardless of which shard published them.
    /// </summary>
    public ValueTask<ImmutableArray<ProxyShopExpirationRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}

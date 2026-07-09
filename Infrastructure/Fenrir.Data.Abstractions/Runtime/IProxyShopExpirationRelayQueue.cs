namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Non-blocking producer handle for the cross-shard proxy-shop rental-extension live-registry mirror
///     relay outbox -- <c>UseInventoryItemService.ResolveProxyShopRentalExtensionAsync</c> only needs this
///     narrow surface, not the concrete <c>ProxyShopExpirationRelayHost</c>'s own lifecycle. Same
///     "non-generic handle for a hot synchronous call site" shape as <see cref="IGuildTribeBroadcastRelayQueue" />
///     -- see that interface's own remarks for why <c>*.Services</c> depends on <c>Fenrir.Data.Abstractions</c>
///     only rather than the concrete Hosting project directly.
/// </summary>
public interface IProxyShopExpirationRelayQueue
{
    /// <summary>
    ///     Non-blocking, synchronous enqueue. Never awaits, never throws on backpressure. Returns false only
    ///     if the bounded channel is full; the cross-shard leg of this one rental extension is then silently
    ///     dropped -- the durable <c>game.OfflineShops.ShopDate</c> write and the caller's own local
    ///     same-shard <c>Zone.TryUpdateProxyShopExpiration</c> attempt have already happened either way, so
    ///     nothing observable to the client changes.
    /// </summary>
    public bool Enqueue(ProxyShopExpirationRelayEntry entry);
}

using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IProxyShopExpirationRelayQueue: records every enqueued cross-shard proxy-shop
///     expiration relay row (append-only) so UseInventoryItemService's proxy-shop rental-extension test suite
///     can assert a relay entry was (or was not) queued, without a real channel/background host.
/// </summary>
internal sealed class FakeProxyShopExpirationRelayQueue : IProxyShopExpirationRelayQueue
{
    public List<ProxyShopExpirationRelayEntry> Enqueued { get; } = [];

    /// <summary>When true, <see cref="Enqueue" /> reports backpressure (bounded channel full) instead of accepting.</summary>
    public bool RejectNext { get; set; }

    public bool Enqueue(ProxyShopExpirationRelayEntry entry)
    {
        if (RejectNext)
        {
            RejectNext = false;
            return false;
        }

        Enqueued.Add(entry);
        return true;
    }
}

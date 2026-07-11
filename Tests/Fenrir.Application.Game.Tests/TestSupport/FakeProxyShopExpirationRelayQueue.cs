using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeProxyShopExpirationRelayQueue : IProxyShopExpirationRelayQueue
{
    public List<ProxyShopExpirationRelayEntry> Enqueued { get; } = [];

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

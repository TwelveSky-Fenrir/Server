namespace Fenrir.Data.Abstractions.Runtime;

public interface IProxyShopExpirationRelayQueue
{

        public bool Enqueue(ProxyShopExpirationRelayEntry entry);
}

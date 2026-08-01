namespace Fenrir.Data.Abstractions.Runtime;

public interface IProxyShopExpirationRelayRepository
    : IClusterRelayBackend<ProxyShopExpirationRelayEntry, ProxyShopExpirationRelayDto>
{
}

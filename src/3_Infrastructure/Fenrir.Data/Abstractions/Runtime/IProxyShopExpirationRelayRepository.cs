namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
/// Dépôt de relais des prolongations d'expiration de proxy-shop cross-shard. Publish/Poll sont hérités tels
/// quels de <see cref="IClusterRelayBackend{TEntry,TDto}"/> (consolidation des 7 relais outbox, WS-C) ; le corps
/// du dépôt et ses procédures <c>runtime.usp_ProxyShopExpirationRelay_Publish/_Poll</c> restent inchangés.
/// </summary>
public interface IProxyShopExpirationRelayRepository
    : IClusterRelayBackend<ProxyShopExpirationRelayEntry, ProxyShopExpirationRelayDto>
{
}

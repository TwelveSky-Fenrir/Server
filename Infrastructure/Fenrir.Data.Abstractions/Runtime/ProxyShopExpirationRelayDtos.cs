using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Everything <c>ProxyShopExpirationRelayHost</c>'s outbound drain loop needs to call
///     <c>usp_ProxyShopExpirationRelay_Publish</c> -- constructed by
///     <c>UseInventoryItemService.ResolveProxyShopRentalExtensionAsync</c> immediately after its own
///     unchanged, synchronous local <c>Zone.TryUpdateProxyShopExpiration</c> attempt (see
///     <see cref="IProxyShopExpirationRelayQueue" /> for that boundary), regardless of whether the local
///     attempt hit or missed.
/// </summary>
public sealed record ProxyShopExpirationRelayEntry(
    byte SourceShardId,
    int CharacterId,
    int NewExpirationDate);

// Ordinal-mapped: ctor order must match usp_ProxyShopExpirationRelay_Poll's SELECT order.
[GenerateDto]
public sealed partial record ProxyShopExpirationRelayDto(
    long RelayId,
    int CharacterId,
    int NewExpirationDate);

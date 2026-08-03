using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class GetProxyShopService(
    IOfflineShopRepository offlineShops,
    ILogger<GetProxyShopService> logger) : IGetProxyShopService
{
    private const int NoError = 0;
    private const int BackingQueryFailedCode = 100;
    private const int NotFoundOrNotOpenCode = 101;
    private const int BackingQueryNonZeroResultCode = 102;
    private const int ShopNotClosedCode = 1;

    public ValueTask<GetProxyShopResponse> GetAsync(GetProxyShopRequest packet, Zone zone, int characterId,
        CancellationToken cancellationToken) =>
        packet.Sort == 1
            ? GetOwnClosedShopAsync(zone, characterId, cancellationToken)
            : BrowseOpenShopByNameAsync(packet, zone, cancellationToken);

    private async ValueTask<GetProxyShopResponse> GetOwnClosedShopAsync(Zone zone, int characterId,
        CancellationToken cancellationToken)
    {
        OfflineShopRowDto? shop;
        IReadOnlyList<OfflineShopItemRowDto> items;
        try
        {
            (shop, items) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Get proxy shop (sort 1) round trip failed for character {CharacterId}",
                characterId);
            return Failure(BackingQueryFailedCode);
        }

        if (shop is null)
        {
            logger.LogDebug("Get proxy shop (sort 1): character {CharacterId} has no offline-shop record",
                characterId);
            return Failure(BackingQueryNonZeroResultCode);
        }

        if (shop.ShopState != 0)
        {
            logger.LogDebug(
                "Get proxy shop (sort 1) rejected: character {CharacterId} shop is not closed (state {ShopState})",
                characterId, shop.ShopState);
            return Failure(ShopNotClosedCode);
        }

        var ownName = zone.TryGetPlayer(characterId, out var self) && self is not null ? self.Name : string.Empty;
        logger.LogDebug(
            "Get proxy shop (sort 1): character {CharacterId} loaded their own closed proxy shop ({ItemCount} items)",
            characterId, items.Count);
        return Success(1, ProxyShopWireMapper.Build(ownName, shop, items));
    }

    private async ValueTask<GetProxyShopResponse> BrowseOpenShopByNameAsync(GetProxyShopRequest packet, Zone zone,
        CancellationToken cancellationToken)
    {
        if (!zone.TryFindProxyShopByName(packet.AvatarName, out var entry))
        {
            logger.LogDebug(
                "Get proxy shop (sort {Sort}) rejected: no open proxy shop named {AvatarName} in zone {MapId}",
                packet.Sort, packet.AvatarName, zone.MapId);
            return Failure(NotFoundOrNotOpenCode);
        }

        OfflineShopRowDto? shop;
        IReadOnlyList<OfflineShopItemRowDto> items;
        try
        {
            (shop, items) = await offlineShops.GetByCharacterAsync(entry.CharacterId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Get proxy shop (sort {Sort}) item round trip failed for character {CharacterId}",
                packet.Sort, entry.CharacterId);
            return Failure(BackingQueryFailedCode);
        }

        if (shop is null)
        {
            logger.LogDebug(
                "Get proxy shop (sort {Sort}) rejected: {AvatarName}'s shop closed between registry lookup and read",
                packet.Sort, packet.AvatarName);
            return Failure(NotFoundOrNotOpenCode);
        }

        logger.LogDebug(
            "Get proxy shop (sort {Sort}): resolved {AvatarName}'s open proxy shop ({ItemCount} items)",
            packet.Sort, packet.AvatarName, items.Count);
        return Success(packet.Sort, ProxyShopWireMapper.Build(entry.OwnerName, shop, items));
    }

    private static GetProxyShopResponse Success(int sort, ProxyShopUserInfo payload) =>
        new() { Result = sort, Sort = NoError, ProxyUser = payload };

    private static GetProxyShopResponse Failure(int errorCode) =>
        new() { Result = 0, Sort = errorCode, ProxyUser = ProxyShopWireMapper.Build(string.Empty, null, []) };
}

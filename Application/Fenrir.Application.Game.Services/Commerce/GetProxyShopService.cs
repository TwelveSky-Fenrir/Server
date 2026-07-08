using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class GetProxyShopService(IOfflineShopRepository offlineShops, ICharacterRepository characters,
    ILogger<GetProxyShopService> logger) : IGetProxyShopService
{
    public async ValueTask<GetProxyShopResponse> GetAsync(GetProxyShopRequest packet, Zone zone, int characterId,
        CancellationToken cancellationToken)
    {
        if (packet.Sort is 1 or 2)
        {
            var (shop, items) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
            var name = zone.TryGetPlayer(characterId, out var self) && self is not null
                ? self.Name
                : packet.AvatarName;

            if (shop is null)
                logger.LogDebug("Get proxy shop: character {CharacterId} has no proxy shop of their own",
                    characterId);
            else
                logger.LogDebug("Get proxy shop: character {CharacterId} fetched their own proxy shop ({ItemCount} items)",
                    characterId, items.Count);

            return new GetProxyShopResponse
            {
                Result = shop is null ? 101 : 0, Sort = packet.Sort,
                ProxyUser = ProxyShopWireMapper.Build(name, shop, items)
            };
        }

        var targetId = await characters.GetIdByNameAsync(packet.AvatarName, cancellationToken);
        if (targetId is null)
        {
            logger.LogDebug(
                "Get proxy shop rejected: character {CharacterId} target {TargetAvatarName} does not exist",
                characterId, packet.AvatarName);
            return new GetProxyShopResponse
            {
                Result = 101, Sort = packet.Sort, ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, null, [])
            };
        }

        var (targetShop, targetItems) = await offlineShops.GetByCharacterAsync(targetId.Value, cancellationToken);
        if (targetShop is not { ShopState: 1 })
        {
            logger.LogDebug(
                "Get proxy shop rejected: character {CharacterId} target {TargetCharacterId} has no open proxy shop",
                characterId, targetId.Value);
            return new GetProxyShopResponse
            {
                Result = 101, Sort = packet.Sort, ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, null, [])
            };
        }

        logger.LogDebug(
            "Get proxy shop: character {CharacterId} fetched target {TargetCharacterId}'s proxy shop ({ItemCount} items)",
            characterId, targetId.Value, targetItems.Count);
        return new GetProxyShopResponse
        {
            Result = 0, Sort = packet.Sort,
            ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, targetShop, targetItems)
        };
    }
}

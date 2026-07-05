using Fenrir.Application.Game.Handlers.Commerce;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_GET_DEPUTY_PSHOP_SEND (opcode 108), extracted from <see cref="GetProxyShopHandler" />.</summary>
public interface IGetProxyShopService
{
    /// <summary>
    ///     <c>Sort</c> 1/2 resolve the CALLER's own shop regardless of ShopState (so a closed shop's owner can
    ///     still inspect/withdraw); <c>Sort</c> 3 resolves <c>AvatarName</c> and requires the target's shop to
    ///     be OPEN.
    /// </summary>
    ValueTask<GetProxyShopResponse> GetAsync(GetProxyShopRequest packet, Zone zone, int characterId,
        CancellationToken cancellationToken);
}

public sealed class GetProxyShopService(IOfflineShopRepository offlineShops, ICharacterRepository characters)
    : IGetProxyShopService
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

            return new GetProxyShopResponse
            {
                Result = shop is null ? 101 : 0, Sort = packet.Sort,
                ProxyUser = ProxyShopWireMapper.Build(name, shop, items)
            };
        }

        var targetId = await characters.GetIdByNameAsync(packet.AvatarName, cancellationToken);
        if (targetId is null)
            return new GetProxyShopResponse
            {
                Result = 101, Sort = packet.Sort, ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, null, [])
            };

        var (targetShop, targetItems) = await offlineShops.GetByCharacterAsync(targetId.Value, cancellationToken);
        if (targetShop is not { ShopState: 1 })
            return new GetProxyShopResponse
            {
                Result = 101, Sort = packet.Sort, ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, null, [])
            };

        return new GetProxyShopResponse
        {
            Result = 0, Sort = packet.Sort,
            ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, targetShop, targetItems)
        };
    }
}

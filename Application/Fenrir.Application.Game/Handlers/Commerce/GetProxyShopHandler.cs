using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_DEPUTY_PSHOP_SEND (opcode 108) -- fetch a deputy (offline/proxy) shop's contents. Gated to
///     zone 37. <c>Sort</c> 1/2 resolve the CALLER's own shop regardless of ShopState (so a closed shop's
///     owner can still inspect/withdraw); <c>Sort</c> 3 resolves <c>AvatarName</c> and requires the
///     target's shop to be OPEN.
/// </summary>
public sealed class GetProxyShopHandler(IOfflineShopRepository offlineShops, ICharacterRepository characters)
    : IAsyncPacketHandler<GetProxyShopRequest>
{
    public async ValueTask HandleAsync(GetProxyShopRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort is 1 or 2)
        {
            var (shop, items) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
            var name = zone.TryGetPlayer(characterId, out var self) && self is not null ? self.Name : packet.AvatarName;

            session.Send(new GetProxyShopResponse
            {
                Result = shop is null ? 101 : 0, Sort = packet.Sort,
                ProxyUser = ProxyShopWireMapper.Build(name, shop, items)
            });
            return;
        }

        var targetId = await characters.GetIdByNameAsync(packet.AvatarName, cancellationToken);
        if (targetId is null)
        {
            session.Send(new GetProxyShopResponse
            {
                Result = 101, Sort = packet.Sort, ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, null, [])
            });
            return;
        }

        var (targetShop, targetItems) = await offlineShops.GetByCharacterAsync(targetId.Value, cancellationToken);
        if (targetShop is not { ShopState: 1 })
        {
            session.Send(new GetProxyShopResponse
            {
                Result = 101, Sort = packet.Sort, ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, null, [])
            });
            return;
        }

        session.Send(new GetProxyShopResponse
        {
            Result = 0, Sort = packet.Sort,
            ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, targetShop, targetItems)
        });
    }
}

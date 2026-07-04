using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_DEPUTY_PSHOP_SEND (opcode 108, contracts/04_commerce.md, verified <c>S07_MyGame09.cpp:96-193/506-555</c>)
///     -- fetch a deputy (offline/proxy) shop's contents. Gated to zone 37 under <c>PPSHOP_V2</c> (verified,
///     same gate every proxy-shop opcode shares).
/// </summary>
/// <remarks>
///     The legacy's three sorts exist only because of the ts25extra IPC round trip's own caching model
///     (Sort 1 preloads into that cache for a later open call to read) -- Fenrir talks to SQL directly, so
///     all three are answered uniformly: <c>Sort</c> 1/2 resolve the CALLER's own shop by CharacterId
///     regardless of ShopState (so a closed shop's owner can still inspect/withdraw); <c>Sort</c> 3
///     resolves <c>AvatarName</c> via <see cref="CharacterRepository.GetIdByNameAsync" /> and requires the
///     target's shop to be OPEN (ShopState=1, matching the verified source's gate for "get other").
/// </remarks>
public sealed class GetProxyShopHandler(IOfflineShopRepository offlineShops, ICharacterRepository characters)
    : IAsyncPacketHandler<GetProxyShopRequest>
{
    public async ValueTask HandleAsync(GetProxyShopRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Fenrir.Application.Game.World.Zone zone)
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
                { Result = 101, Sort = packet.Sort, ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, null, []) });
            return;
        }

        var (targetShop, targetItems) = await offlineShops.GetByCharacterAsync(targetId.Value, cancellationToken);
        if (targetShop is not { ShopState: 1 })
        {
            session.Send(new GetProxyShopResponse
                { Result = 101, Sort = packet.Sort, ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, null, []) });
            return;
        }

        session.Send(new GetProxyShopResponse
        {
            Result = 0, Sort = packet.Sort, ProxyUser = ProxyShopWireMapper.Build(packet.AvatarName, targetShop, targetItems)
        });
    }
}

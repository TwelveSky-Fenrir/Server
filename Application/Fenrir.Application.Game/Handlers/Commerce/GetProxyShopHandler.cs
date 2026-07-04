using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_DEPUTY_PSHOP_SEND (opcode 108, contracts/04_commerce.md, verified <c>S07_MyGame09.cpp:
///     96-193/506-555</c>) -- fetch a deputy (offline/proxy) shop's contents. Runtime-gated to zone 37
///     under <c>PPSHOP_V2</c> (verified, same gate every proxy-shop opcode shares).
/// </summary>
/// <remarks>
///     SCOPE SIMPLIFICATION (documented): the legacy's three sorts (1 = "ask for open/close" via a
///     preload-into-IPC-cache side effect only meaningful because a LATER open call reads that cache;
///     2 = "get self"; 3 = "get other", both AvatarName-matched) exist only because of the ts25extra IPC
///     round trip's own caching model -- Fenrir talks to SQL directly, so there is no preload step to
///     reproduce. All three sorts are answered uniformly here: <c>Sort</c> 1/2 resolve the CALLER's OWN
///     shop by CharacterId (regardless of ShopState -- useful for a closed shop's own owner to inspect
///     before retrieving/withdrawing), <c>Sort</c> 3 resolves <c>AvatarName</c> via
///     <see cref="CharacterRepository.GetIdByNameAsync" /> and requires the target's shop to be OPEN
///     (ShopState=1, matching the verified source's own gate for "get other").
/// </remarks>
public sealed class GetProxyShopHandler(OfflineShopRepository offlineShops, CharacterRepository characters)
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

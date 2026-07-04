using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_END_PSHOP_SEND (opcode 32, contracts/04_commerce.md, verified <c>S04_MyWork02.cpp:6351-6383</c>).
///     <c>Sort</c> 1 closes the live personal shop, replying only if one was actually open (verified legacy
///     no-op otherwise, not an oversight). <c>Sort</c> 2 closes the offline/deputy shop
///     (<see cref="OfflineShopRepository.SetStateAsync" />, ShopState only -- items/money stay attached) and
///     sends no unicast reply either, matching the legacy's own <c>CloseProxyShop</c> (only a world
///     broadcast, not modeled here -- reproduced exactly, not a gap).
/// </summary>
public sealed class CloseShopStallHandler(IOfflineShopRepository offlineShops)
    : IAsyncPacketHandler<CloseShopStallRequest>
{
    public async ValueTask HandleAsync(CloseShopStallRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        switch (packet.Sort)
        {
            case 1 when state.PshopOpen:
                state.PshopOpen = false;
                session.Send(new CloseShopStallResponse { Result = 1 });
                break;
            case 2:
                await offlineShops.SetStateAsync(characterId, shopState: 0, cancellationToken);
                break;
        }
    }
}

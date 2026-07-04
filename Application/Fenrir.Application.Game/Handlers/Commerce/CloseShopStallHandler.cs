using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_END_PSHOP_SEND (opcode 32, contracts/04_commerce.md, verified <c>S04_MyWork02.cpp:6351-6383</c>).
///     <c>Sort</c> 1 = close the live personal shop (replies ONLY if it was actually open, matching the
///     legacy's own "neither reply nor Quit()" no-op when it wasn't -- verified, not an oversight). <c>Sort</c>
///     2 = close the offline/deputy shop (<see cref="OfflineShopRepository.SetStateAsync" />, ShopState
///     only -- items/money stay attached, see that proc's own header): the legacy's own
///     <c>CloseProxyShop</c> success path sends NO unicast reply at all either (only a world broadcast this
///     pass does not model, see <see cref="OpenShopStallHandler" />'s own open-issue posture on cosmetic
///     broadcasts) -- reproduced exactly, not a gap.
/// </summary>
public sealed class CloseShopStallHandler(OfflineShopRepository offlineShops)
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

using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_SET_DEPUTY_PSHOP_SEND (opcode 109). <c>BuySort</c> 1 = RETRIEVE an unsold item from the caller's
///     own closed shop back to inventory; <c>BuySort</c> 2 = PURCHASE from another character's open shop.
///     Only the buyer/retriever is ever a live participant -- the seller's shop lives purely in SQL, so no
///     dual-lock is needed here (unlike <c>BuyShopItemHandler</c>'s live-PShop twin).
/// </summary>
public sealed class UpdateProxyShopHandler(IUpdateProxyShopService service)
    : IAsyncPacketHandler<UpdateProxyShopRequest>
{
    public async ValueTask HandleAsync(UpdateProxyShopRequest packet, IPacketSession session,
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

        var validation = service.Validate(packet);
        if (validation.Abort)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = packet.BuySort == 1
                ? await service.RetrieveAsync(packet, zone, state, characterId, validation.SlotIndex,
                    validation.ItemDefinition!, cancellationToken)
                : await service.PurchaseAsync(packet, zone, state, characterId, validation.SlotIndex,
                    validation.ItemDefinition!, cancellationToken);

            if (result is null)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(result.Value);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}

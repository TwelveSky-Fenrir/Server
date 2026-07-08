using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_SET_DEPUTY_PSHOP_SEND (opcode 109). <c>BuySort</c> 1 = RETRIEVE an unsold item from the caller's
///     own closed shop back to inventory; <c>BuySort</c> 2 = PURCHASE from another character's open shop.
///     Only the buyer/retriever is ever a live participant -- the seller's shop lives purely in SQL, so no
///     dual-lock is needed here (unlike <c>BuyShopItemHandler</c>'s live-PShop twin).
/// </summary>
public sealed class UpdateProxyShopHandler(IUpdateProxyShopService service, ILogger<UpdateProxyShopHandler> logger)
    : IAsyncPacketHandler<UpdateProxyShopRequest>
{
    public async ValueTask HandleAsync(UpdateProxyShopRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "UpdateProxyShop: session {SessionId} character {CharacterId} buySort {BuySort} seller {AvatarName}",
            session.SessionId, characterId, packet.BuySort, packet.AvatarName);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            logger.LogWarning(
                "Update proxy shop rejected: character {CharacterId} is outside the market district (zone {MapId}) -- session will be disconnected",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var validation = service.Validate(packet);
        if (validation.Abort)
        {
            logger.LogWarning(
                "Update proxy shop rejected: character {CharacterId} request failed structural validation -- session will be disconnected",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = packet.BuySort == 1
                ? await service.RetrieveAsync(packet, zone, state, characterId, accountId, validation.SlotIndex,
                    validation.ItemDefinition!, cancellationToken)
                : await service.PurchaseAsync(packet, zone, state, characterId, accountId, validation.SlotIndex,
                    validation.ItemDefinition!, cancellationToken);

            if (result is null)
            {
                logger.LogWarning(
                    "Update proxy shop rejected: character {CharacterId} buySort {BuySort} failed structural validation post-lock -- session will be disconnected",
                    characterId, packet.BuySort);
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

using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class UpdateProxyShopHandler(IUpdateProxyShopService service, ILogger<UpdateProxyShopHandler> logger)
    : IAsyncPacketHandler<UpdateProxyShopRequest>
{
    public async ValueTask HandleAsync(UpdateProxyShopRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
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
                "Update proxy shop rejected: character {CharacterId} is outside the market district (zone {MapId}), terminating session (Server/ts25zone/S04_MyWork02.cpp:13649 IsValidZoneForProxyShop -> Quit())",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var validation = service.Validate(packet);
        if (validation.Abort)
        {
            logger.LogWarning(
                "Update proxy shop rejected: character {CharacterId} request failed structural validation",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            if (validation.BusinessFailure)
            {
                session.Send(await service.BuildBusinessFailureAsync(packet, state, characterId, cancellationToken));
                return;
            }

            var result = packet.BuySort == 1
                ? await service.RetrieveAsync(packet, zone, state, characterId, accountId, validation.SlotIndex,
                    validation.ItemDefinition!, cancellationToken)
                : await service.PurchaseAsync(packet, zone, state, characterId, accountId, validation.SlotIndex,
                    validation.ItemDefinition!, cancellationToken);

            if (result is null)
            {
                logger.LogWarning(
                    "Update proxy shop rejected: character {CharacterId} buySort {BuySort} failed structural validation post-lock",
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

using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class OpenShopStallHandler(IOpenShopStallService service, ILogger<OpenShopStallHandler> logger)
    : IAsyncPacketHandler<OpenShopStallRequest>
{
    public const short PshopZoneNumber = ProxyShopZonePolicy.ZoneNumber;

    public async ValueTask HandleAsync(OpenShopStallRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "OpenShopStall: session {SessionId} character {CharacterId} sort {Sort} name {ShopName}",
            session.SessionId, characterId, packet.Sort, packet.PshopInfo.Name);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != PshopZoneNumber)
        {
            logger.LogWarning(
                "Open shop stall rejected: character {CharacterId} is outside the market district (zone {MapId}); terminating session",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var prepared = await service.PrepareAsync(packet, state, cancellationToken);
        switch (prepared.Outcome)
        {
            case OpenShopStallPrepareOutcome.Abort:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case OpenShopStallPrepareOutcome.LiveOpened:
                if (!await zone.PostPshopCommandAndWaitAsync(new PshopZoneCommand(characterId, false,
                        OpenListing: prepared.Listing, DisableAutoHunt: true), cancellationToken))
                {
                    logger.LogError(
                        "Zone {MapId} pshop inbox full: failed to mirror personal-shop open for character {CharacterId}",
                        zone.MapId, characterId);
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                }

                session.Send(prepared.Response!.Value);

                if (!await zone.PostPshopCommandAndWaitAsync(new PshopZoneCommand(characterId, false,
                        BroadcastOpenAction: true), cancellationToken))
                {
                    logger.LogError(
                        "Zone {MapId} pshop inbox full: failed to broadcast personal-shop open for character {CharacterId}",
                        zone.MapId, characterId);
                    zoneSession.Abort(DisconnectReason.Faulted);
                }

                return;
            case OpenShopStallPrepareOutcome.Blocked:
                session.Send(prepared.Response!.Value);
                return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var opened = await service.OpenProxyShopAsync(packet, zone, state, characterId, accountId,
                prepared.Listing, prepared.OfflineItems!, cancellationToken);
            if (opened.Snapshot is { } snapshot)
                session.Send(snapshot);

            session.Send(opened.Response);

            foreach (var clearedSlot in opened.InventoryClears)
                session.Send(new AddInventoryItemResponse
                {
                    Result = 0,
                    ItemIndex = 0,
                    Page = clearedSlot.Page,
                    Index = clearedSlot.Index,
                    Xy = 0,
                    Quantity = 0,
                    Value = 0,
                    Serial = 0,
                    Socket = [0, 0, 0],
                    Expire = 0
                });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}

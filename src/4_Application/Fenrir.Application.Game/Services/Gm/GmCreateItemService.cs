using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Sessions;
using Fenrir.Core.Packets.Shared;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmCreateItemService(
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<GmCreateItemService> logger) : IGmCreateItemService
{
    private const int RejectedResult = 1;

    private const int AcceptedResult = 0;

    private const int StackableQuantityRejectedResult = 2;

    private const int MinItemId = 2;
    private const int MaxItemId = 99999;

    private const short ItemCreateEventCode = 1;

    private const byte ItemCreateOutcome = 1;

    public async ValueTask HandleAsync(int sort, byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Admin))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Admin-tier spawn-item command (sort {Sort}) without sufficient privilege -- forcing logout, no reply",
                zoneSession.CharacterId, sort);
            zoneSession.Abort(DisconnectReason.GmCommandLogout);
            return;
        }

        int itemId;
        int requestedQuantity;
        if (sort == 523)
        {
            if (!GmCreateItemQuantityPayload.TryRead(data, out var quantityPayload))
            {
                zoneSession.Abort(DisconnectReason.Malformed);
                return;
            }

            itemId = quantityPayload.ItemId;
            requestedQuantity = quantityPayload.Quantity;
        }
        else
        {
            if (!GmCreateItemPayload.TryRead(data, out var basePayload))
            {
                zoneSession.Abort(DisconnectReason.Malformed);
                return;
            }

            itemId = basePayload.ItemId;
            requestedQuantity = 0;
        }

        if (itemId is < MinItemId or > MaxItemId || !worldData.ItemsById.TryGetValue(itemId, out var definition))
        {
            logger.LogInformation(
                "Character {CharacterId} spawn-item rejected: item {ItemId} out of range or not in the catalog (sort {Sort})",
                zoneSession.CharacterId, itemId, sort);
            zoneSession.Send(new GenericActionResponse
                { Result = RejectedResult, Sort = sort, Data = data, RuneValue = 0 });
            return;
        }

        var isStackable = ContainerMatrix.IsStackableSort(definition.Item.Sort);
        var drops = ResolveDrops(sort, itemId, requestedQuantity, definition.Item.Sort);

        if (drops.Count > 0 &&
            !await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, DropItems: [..drops]), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped spawn-item mirror for character {CharacterId} (item {ItemId}) -- reporting accepted regardless, matching legacy's own result-code masking",
                zone.MapId, state.CharacterId, itemId);

        foreach (var drop in drops)
            await eventLog.LogAsync(ItemCreateEventCode, EventLogCategory.ItemCreate, zoneSession.AccountId,
                zoneSession.CharacterId, null, null, null, null, null, drop.ItemId, drop.Quantity, ItemCreateOutcome,
                $"GmName={state.Name};Value=0;CheckImprove={definition.Item.CheckImprove};CheckHighImprove={definition.Item.CheckHighImprove};CheckSetItem={definition.Item.CheckSetItem}",
                cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} spawn-item applied: item {ItemId}, {ObjectCount} object(s) created (sort {Sort})",
            state.CharacterId, itemId, drops.Count, sort);

        var resultCode = isStackable && drops.Count == 0 ? StackableQuantityRejectedResult : AcceptedResult;
        zoneSession.Send(new GenericActionResponse { Result = resultCode, Sort = sort, Data = data, RuneValue = 0 });
    }

    private static List<TribeGroundItemDrop> ResolveDrops(int sort, int itemId, int requestedQuantity,
        byte itemCatalogSort)
    {
        var drops = new List<TribeGroundItemDrop>();

        if (ContainerMatrix.IsStackableSort(itemCatalogSort))
        {
            int quantity;
            if (sort != 523 || requestedQuantity == 0)
                quantity = GroundItemPickupPolicy.MaxStackQuantity;
            else if (requestedQuantity is >= 1 and <= GroundItemPickupPolicy.MaxStackQuantity)
                quantity = requestedQuantity;
            else
                quantity = 0;

            if (quantity > 0)
                drops.Add(new TribeGroundItemDrop(itemId, quantity, GroundItemEntity.GmCreateItemDropSort));
        }
        else
        {
            var resolvedCount = sort != 523 || requestedQuantity == 0 ? 1 : requestedQuantity;
            for (var i = 0; i < resolvedCount; i++)
                drops.Add(new TribeGroundItemDrop(itemId, 1, GroundItemEntity.GmCreateItemDropSort));
        }

        return drops;
    }
}

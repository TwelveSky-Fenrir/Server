using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class CostumeStellarCoreUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<CostumeStellarCoreUseItemHandler> logger) : IUseItemHandler
{

        private const short WardrobeItemConsumedEventCode = 1;

        private const short WardrobeGrantedEventCode = 2;

    private const byte SuccessOutcome = 1;

        public static bool ClaimsItem(int itemId)
    {
        return CostumeStellarCoreWhitelist.ClaimsItem(itemId);
    }

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var itemId = context.Item.ItemId;

        if (CostumeStellarCoreWhitelist.IsValidCostume(itemId))
            return await HandleCostumeAsync(context, cancellationToken);

        if (CostumeStellarCoreWhitelist.IsValidStellarCore(itemId))
            return await HandleStellarCoreAsync(context, cancellationToken);

        logger.LogWarning(
            "Character {CharacterId} op23 costume/stellar-core dispatch reached for item {ItemId}, which matches neither whitelist -- registry routing bug",
            context.CharacterId, itemId);
        return UseItemResponses.Fail(context.Page, context.Index);
    }

    private async ValueTask<UseInventoryItemResponse> HandleCostumeAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var item = context.Item;

        var resolved = CostumeStellarCoreGrantResolver.Resolve(state.CostumeWardrobe, item.ItemId);

        switch (resolved.Outcome)
        {
            case CostumeStellarCoreGrantResolver.Outcome.AlreadyWorn:
                logger.LogDebug(
                    "Character {CharacterId} op23 costume grant rejected: item {ItemId} already worn",
                    context.CharacterId, item.ItemId);
                return UseItemResponses.Fail(context.Page, context.Index);

            case CostumeStellarCoreGrantResolver.Outcome.NoFreeSlot:
                logger.LogInformation(
                    "Character {CharacterId} op23 costume grant: wardrobe full for item {ItemId} -- disconnecting (legacy Quit(), no acknowledgment)",
                    context.CharacterId, item.ItemId);
                if (state.Session is ClientSession client)
                    client.Abort(DisconnectReason.WardrobeFull);
                return UseItemResponses.Fail(context.Page, context.Index);

            case CostumeStellarCoreGrantResolver.Outcome.Success:
            {
                var slot = resolved.SlotIndex;
                var packedCostumeDate = ItemValueCodec.Encode(item.Enchant, item.Combine, item.Refine, item.Socket);

                await eventLog.LogAsync(WardrobeItemConsumedEventCode, EventLogCategory.ItemUse, context.AccountId,
                    context.CharacterId, null, null, null, null, null, item.ItemId, item.Quantity, SuccessOutcome,
                    $"Serial={item.Serial};ExpireDate={item.ExpireDate}", cancellationToken);

                await eventLog.LogAsync(WardrobeGrantedEventCode, EventLogCategory.ItemUse, context.AccountId,
                    context.CharacterId, null, null, null, null, null, item.ItemId, null, SuccessOutcome,
                    $"WardrobeSlot={slot}", cancellationToken);

                await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
                    context.Index, item, 0, null, cancellationToken);

                if (!context.Zone.PostCostumeCommand(new CostumeZoneCommand(context.CharacterId,
                        WardrobeSlotGranted: slot, GrantedItemId: item.ItemId, GrantedCostumeDate: packedCostumeDate,
                        GrantedExpireDate: item.ExpireDate)))
                    logger.LogError(
                        "Zone {MapId} costume inbox full: dropped op23 costume-grant mirror for character {CharacterId} -- item already durably consumed, wardrobe slot {Slot} will NOT reflect the grant until a future costume mutation resynchronizes it",
                        context.Zone.MapId, context.CharacterId, slot);

                logger.LogInformation(
                    "Character {CharacterId} op23 costume grant applied: item {ItemId} -> wardrobe slot {Slot}",
                    context.CharacterId, item.ItemId, slot);

                return UseItemResponses.Success(context.Page, context.Index, slot);
            }

            default:
                return UseItemResponses.Fail(context.Page, context.Index);
        }
    }

    private async ValueTask<UseInventoryItemResponse> HandleStellarCoreAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var item = context.Item;

        var resolved = CostumeStellarCoreGrantResolver.Resolve(state.StellarCoreWardrobe, item.ItemId);

        switch (resolved.Outcome)
        {
            case CostumeStellarCoreGrantResolver.Outcome.AlreadyWorn:
                logger.LogDebug(
                    "Character {CharacterId} op23 stellar-core grant rejected: item {ItemId} already worn",
                    context.CharacterId, item.ItemId);
                return UseItemResponses.Fail(context.Page, context.Index);

            case CostumeStellarCoreGrantResolver.Outcome.NoFreeSlot:
                logger.LogInformation(
                    "Character {CharacterId} op23 stellar-core grant: wardrobe full for item {ItemId} -- disconnecting (legacy Quit(), no acknowledgment)",
                    context.CharacterId, item.ItemId);
                if (state.Session is ClientSession client)
                    client.Abort(DisconnectReason.WardrobeFull);
                return UseItemResponses.Fail(context.Page, context.Index);

            case CostumeStellarCoreGrantResolver.Outcome.Success:
            {
                var slot = resolved.SlotIndex;

                await eventLog.LogAsync(WardrobeItemConsumedEventCode, EventLogCategory.ItemUse, context.AccountId,
                    context.CharacterId, null, null, null, null, null, item.ItemId, item.Quantity, SuccessOutcome,
                    $"Serial={item.Serial};ExpireDate={item.ExpireDate}", cancellationToken);

                await eventLog.LogAsync(WardrobeGrantedEventCode, EventLogCategory.ItemUse, context.AccountId,
                    context.CharacterId, null, null, null, null, null, item.ItemId, null, SuccessOutcome,
                    $"WardrobeSlot={slot}", cancellationToken);

                await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
                    context.Index, item, 0, null, cancellationToken);

                if (!context.Zone.PostStellarCoreCommand(new StellarCoreZoneCommand(context.CharacterId,
                        WardrobeSlotGranted: slot, GrantedItemId: item.ItemId, GrantedExpireDate: item.ExpireDate)))
                    logger.LogError(
                        "Zone {MapId} stellar-core inbox full: dropped op23 stellar-core-grant mirror for character {CharacterId} -- item already durably consumed, wardrobe slot {Slot} will NOT reflect the grant until a future stellar-core mutation resynchronizes it",
                        context.Zone.MapId, context.CharacterId, slot);

                logger.LogInformation(
                    "Character {CharacterId} op23 stellar-core grant applied: item {ItemId} -> wardrobe slot {Slot}",
                    context.CharacterId, item.ItemId, slot);

                return UseItemResponses.Success(context.Page, context.Index, slot);
            }

            default:
                return UseItemResponses.Fail(context.Page, context.Index);
        }
    }
}

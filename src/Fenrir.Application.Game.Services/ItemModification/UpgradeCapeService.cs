using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class UpgradeCapeService(
    ICharacterRepository characters,
    IEventLogQueue eventLogQueue,
    IWorldNoticeService worldNotice,
    WorldDataCache worldData,
    ILogger<UpgradeCapeService> logger)
    : IUpgradeCapeService
{
    private const short UpgradeCapeEventCode = 127;

    private const byte SuccessOutcome = 0;

    private const byte FailedOutcome = 1;

    private const int LuckyUpgradeStatSort = 29;

    public async ValueTask<UpgradeCapeResult> UpgradeAsync(UpgradeCapeRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1) ||
            page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page2, index2))
            return AbortAndDisconnect(state, characterId, "invalid inventory slot");

        var today = GameDate.Today();
        if (!RentedInventoryPageGate.IsPageAccessible(page1, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(page2, state.InventoryDate, today))
            return AbortAndDisconnect(state, characterId, "expired inventory page");

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var materialStack = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (targetStack is not { } target || materialStack is not { } material ||
            target.Quantity <= 0 || material.Quantity <= 0 ||
            !worldData.ItemsById.ContainsKey(target.ItemId) || !worldData.ItemsById.ContainsKey(material.ItemId))
            return AbortAndDisconnect(state, characterId, "missing or invalid target/material item");

        var luck = state.Stats?.Luck ?? 0;
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var premiumActive = state.PremiumExpireUtc >= nowUnixSeconds;
        var resolved = CapeUpgradeResolver.Resolve(target.ItemId, material.ItemId, luck, state.HighItemValue,
            premiumActive, SystemRandomSource.Instance);

        if (resolved.Outcome == CapeUpgradeResolver.Outcome.Rejected)
            return AbortAndDisconnect(state, characterId, "invalid cape-upgrade eligibility");

        ItemDefinition? resultDefinition = null;
        if (resolved.Succeeded && !worldData.ItemsById.TryGetValue(resolved.NewItemId, out resultDefinition))
            return AbortAndDisconnect(state, characterId, "cape-upgrade result item is missing");

        var remainingMaterialQuantity = material.Quantity - 1;
        var newMaterialStack = remainingMaterialQuantity > 0
            ? material with { Quantity = remainingMaterialQuantity }
            : (ItemStack?)null;

        var newTargetStack = resolved.Succeeded ? target with { ItemId = resolved.NewItemId } : target;

        ImmutableDictionary<byte, ItemStack> projectedTargetContainer;
        ImmutableDictionary<byte, ItemStack> projectedMaterialContainer;

        if (page1 == page2)
        {
            var combined = ApplySlotChange(state.Inventory.GetContainer((byte)page1), (byte)index1, newTargetStack);
            combined = ApplySlotChange(combined, (byte)index2, newMaterialStack);
            projectedTargetContainer = combined;
            projectedMaterialContainer = combined;
        }
        else
        {
            projectedTargetContainer =
                ApplySlotChange(state.Inventory.GetContainer((byte)page1), (byte)index1, newTargetStack);
            projectedMaterialContainer =
                ApplySlotChange(state.Inventory.GetContainer((byte)page2), (byte)index2, newMaterialStack);
        }

        try
        {
            if (page1 == page2)
                await characters.AdjustMoneyAndReplaceContainerAsync(characterId, -resolved.Cost, 0,
                    (byte)page1, ToTvps(projectedTargetContainer), cancellationToken);
            else
                await characters.AdjustMoneyAndReplaceTwoContainersAsync(characterId, -resolved.Cost, 0,
                    (byte)page1, ToTvps(projectedTargetContainer), (byte)page2, ToTvps(projectedMaterialContainer),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            return AbortAfterUncertainPersistence(state, characterId, ex);
        }

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projectedTargetContainer),
                new InventoryContainerSnapshot((byte)page2, projectedMaterialContainer));

        var inventoryResult = await zone.PostInventoryCommandAndWaitForResultAsync(
            new InventoryZoneCommand(characterId, containers, null), cancellationToken);
        if (inventoryResult.Kind != ZoneCommandResultKind.Applied)
            return AbortAfterDurableMutation(state, characterId, "cape-upgrade inventory", inventoryResult);

        if (resolved.ConsumesLuckyCharge)
        {
            var newHighItemValue = state.HighItemValue - 1;
            var tribeResult = await zone.PostTribeProgressCommandAndWaitForResultAsync(
                new TribeProgressZoneCommand(characterId, HighItemValue: newHighItemValue), cancellationToken);
            if (tribeResult.Kind != ZoneCommandResultKind.Applied)
                return AbortAfterDurableMutation(state, characterId, "cape-upgrade lucky-charge", tribeResult);

            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = LuckyUpgradeStatSort, Value = newHighItemValue, Value2 = 0 });
        }

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(UpgradeCapeEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, -(long)resolved.Cost, null, target.ItemId, target.Quantity,
                resolved.Succeeded ? SuccessOutcome : FailedOutcome,
                $"Serial={target.Serial};Material={material.ItemId};NewItemId={(resolved.Succeeded ? resolved.NewItemId : target.ItemId)}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped cape-upgrade audit row for character {CharacterId}",
                characterId);

        var value = resolved.Succeeded
            ? new[]
            {
                newTargetStack.ItemId, target.XPos, target.YPos, target.Quantity, EncodedValue(target), target.Serial
            }
            : [0, 0, 0, 0, 0, 0];

        if (resolved.Succeeded)
            worldNotice.Broadcast($"{state.Name} RANKUP [{resultDefinition!.Item.Name}]!!!");

        return new UpgradeCapeResult(UpgradeCapeOutcome.Applied, resolved.Succeeded, value);
    }

    private static int EncodedValue(ItemStack stack)
    {
        return ItemValueCodec.Encode(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
    }

    private UpgradeCapeResult AbortAndDisconnect(PlayerRuntimeState state, int characterId, string reason)
    {
        logger.LogInformation("Character {CharacterId} cape-upgrade disconnected: {Reason}", characterId, reason);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
        return new UpgradeCapeResult(UpgradeCapeOutcome.Disconnected, false, [0, 0, 0, 0, 0, 0]);
    }

    private UpgradeCapeResult AbortAfterUncertainPersistence(PlayerRuntimeState state, int characterId,
        Exception exception)
    {
        logger.LogError(exception,
            "Character {CharacterId} cape-upgrade persistence failed after submission; durability is uncertain, disconnecting without success response",
            characterId);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
        return new UpgradeCapeResult(UpgradeCapeOutcome.Disconnected, false, [0, 0, 0, 0, 0, 0]);
    }

    private UpgradeCapeResult AbortAfterDurableMutation(PlayerRuntimeState state, int characterId,
        string mutation, ZoneCommandResult result)
    {
        logger.LogError(
            "Character {CharacterId} cape-upgrade persisted but {Mutation} actor mutation was not acknowledged as applied ({Kind}: {Cause}); disconnecting without success response",
            characterId, mutation, result.Kind, result.Cause);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
        return new UpgradeCapeResult(UpgradeCapeOutcome.Disconnected, false, [0, 0, 0, 0, 0, 0]);
    }

    private static ImmutableDictionary<byte, ItemStack> ApplySlotChange(
        ImmutableDictionary<byte, ItemStack> current, byte slot, ItemStack? value)
    {
        return value is { } v ? current.SetItem(slot, v) : current.Remove(slot);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}

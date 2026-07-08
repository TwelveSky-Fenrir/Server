using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for op127, CZ_UP_LEVEL_ITEM_SEND -- extracted from <see cref="UpgradeCapeHandler" />, see
///     that handler's remarks.
/// </summary>
/// <remarks>
///     <c>premiumActive</c> (Server/Header/function.h:449-461, <c>GetDiscountForPremium</c>) is resolved here
///     from <see cref="PlayerRuntimeState.PremiumExpireUtc" /> against the current wall clock and handed to
///     <see cref="CapeUpgradeResolver.Resolve" />, which applies the flat 20% discount to the cost it returns
///     -- see <see cref="CapeUpgradeResolver" />'s own remarks. Unlike that resolver's own doc comment (written
///     before this notice was wired up), the legacy "RANKUP" chat notice (<c>BroadcastNotice</c>,
///     Server/ts25zone/S04_MyWork02.cpp:14746-14750) now IS reproduced, via <see cref="IWorldNoticeService" />
///     -- see that interface's own remarks for why this specific relay (sort 102) has a safe, already-shipped
///     Fenrir equivalent that EnchantItem's/CraftItem's OWN cap/notable-craft notices (a different relay
///     mechanism, sorts 115/2000/2001) do not.
/// </remarks>
public sealed class UpgradeCapeService(
    ICharacterRepository characters,
    IEventLogQueue eventLogQueue,
    IWorldNoticeService worldNotice,
    WorldDataCache worldData,
    ILogger<UpgradeCapeService> logger)
    : IUpgradeCapeService
{
    /// <summary>
    ///     game.EventLog.EventCode for a cape-upgrade attempt -- the wire opcode (op127) itself, same
    ///     "app-owned numbering scheme, caller-interpreted alongside Category" posture as every other
    ///     EventCode in this codebase.
    /// </summary>
    private const short UpgradeCapeEventCode = 127;

    /// <summary>game.EventLog.Outcome for this EventCode: 0 success, 1 failed.</summary>
    private const byte SuccessOutcome = 0;

    private const byte FailedOutcome = 1;

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
            return new UpgradeCapeResult(UpgradeCapeOutcome.Rejected, false, [0, 0, 0, 0, 0, 0]);

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var materialStack = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (targetStack is not { } target || materialStack is not { } material)
            return new UpgradeCapeResult(UpgradeCapeOutcome.Rejected, false, [0, 0, 0, 0, 0, 0]);

        var luck = state.Stats?.Luck ?? 0;
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var premiumActive = state.PremiumExpireUtc >= nowUnixSeconds;
        var resolved = CapeUpgradeResolver.Resolve(target.ItemId, material.ItemId, luck, 0, premiumActive,
            SystemRandomSource.Instance);

        if (resolved.Outcome == CapeUpgradeResolver.Outcome.Rejected)
            return new UpgradeCapeResult(UpgradeCapeOutcome.Rejected, false, [0, 0, 0, 0, 0, 0]);

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
            logger.LogWarning(ex,
                "Character {CharacterId} cape-upgrade AdjustMoney...ReplaceContainer(s)Async failed (treated as insufficient funds)",
                characterId);
            return new UpgradeCapeResult(UpgradeCapeOutcome.Rejected, false, [0, 0, 0, 0, 0, 0]);
        }

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(UpgradeCapeEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, -(long)resolved.Cost, null, target.ItemId, target.Quantity,
                resolved.Succeeded ? SuccessOutcome : FailedOutcome,
                $"Serial={target.Serial};Material={material.ItemId};NewItemId={(resolved.Succeeded ? resolved.NewItemId : target.ItemId)}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped cape-upgrade audit row for character {CharacterId}",
                characterId);

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projectedTargetContainer),
                new InventoryContainerSnapshot((byte)page2, projectedMaterialContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped cape-upgrade mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        var value = resolved.Succeeded
            ? new[]
            {
                newTargetStack.ItemId, index1 % 8, index1 / 8, target.Quantity, EncodedValue(target), target.Serial
            }
            : [0, 0, 0, 0, 0, 0];

        // Server/ts25zone/S04_MyWork02.cpp:14746-14750 -- fires on EVERY ordinary success, not gated by any
        // tier/allow-list (unlike EnchantItem's/CraftItem's own notices), so this is unconditional here too.
        // Content format is byte-for-byte the legacy sprintf: "%s RANKUP [%s]!!!" (avatar name, result item's
        // display name).
        if (resolved.Succeeded && worldData.ItemsById.TryGetValue(resolved.NewItemId, out var resultDefinition))
            worldNotice.Broadcast($"{state.Name} RANKUP [{resultDefinition.Item.Name}]!!!");

        return new UpgradeCapeResult(UpgradeCapeOutcome.Applied, resolved.Succeeded, value);
    }

    private static int EncodedValue(ItemStack stack)
    {
        return ItemValueCodec.Encode(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
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

using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class ClaimDailyRewardService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<ClaimDailyRewardService> logger) : IClaimDailyRewardService
{
    private const int RewardBundleId = 1;

    public async ValueTask<ClaimDailyRewardResponse?> ResolveAndApplyAsync(ClaimDailyRewardRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var today = GameDate.Today();
        var claimState = await characters.GetRewardClaimStateAsync(characterId, today, cancellationToken);
        if (claimState is null)
            return null;

        if (claimState.RewardClaimDate == today || claimState.RewardClaimDay > 6)
        {
            logger.LogInformation(
                "Daily-reward claim denied for character {CharacterId}: already claimed today or cycle exhausted (day {RewardClaimDay})",
                characterId, claimState.RewardClaimDay);
            return new ClaimDailyRewardResponse
                { Result = 1, Value = new int[6], InvenPage = -1, InvenX = -1, InvenY = -1 };
        }

        if (!worldData.RewardBundleItemsByBundleId.TryGetValue(RewardBundleId, out var slots))
            return null;

        var day = claimState.RewardClaimDay;
        var itemId = 0;
        foreach (var slot in slots)
            if (slot.SlotIndex == day + 1)
            {
                itemId = slot.ItemId ?? 0;
                break;
            }

        if (itemId < 1 || !worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
            return null;

        var freeSlot = FindFreeSlot(state.Inventory);
        if (freeSlot is not { } destination)
        {
            logger.LogInformation("Daily-reward claim denied for character {CharacterId}: inventory full",
                characterId);
            return new ClaimDailyRewardResponse
                { Result = 2, Value = new int[6], InvenPage = -1, InvenX = -1, InvenY = -1 };
        }

        var coupon = itemDefinition.Item.Sort == 99 ? 1 : 0;
        var newStack = new ItemStack(itemId, coupon, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var projectedContainer =
            state.Inventory.GetContainer(destination.Container).SetItem(destination.Slot, newStack);

        try
        {
            await characters.ClaimDailyRewardAsync(characterId, today, destination.Container,
                ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} daily-reward claim ClaimDailyRewardAsync failed (treated as already claimed)",
                characterId);
            return new ClaimDailyRewardResponse
                { Result = 1, Value = new int[6], InvenPage = -1, InvenX = -1, InvenY = -1 };
        }

        var response = new ClaimDailyRewardResponse
        {
            Result = 0,
            Value = [itemId, 0, 0, coupon, 0, 0],
            InvenPage = destination.Container,
            InvenX = 0,
            InvenY = 0
        };

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot(destination.Container, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped daily-reward mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} claimed daily reward day {RewardClaimDay}: item {ItemId} into container {Container}",
            characterId, day, itemId, destination.Container);

        return response;
    }

    private static (byte Container, byte Slot)? FindFreeSlot(InventoryState inventory)
    {
        for (byte slot = 0; slot <= 63; slot++)
            if (inventory.GetSlot(ContainerMatrix.InventoryPage0, slot) is null)
                return (ContainerMatrix.InventoryPage0, slot);

        for (byte slot = 0; slot <= 63; slot++)
            if (inventory.GetSlot(ContainerMatrix.InventoryPage1, slot) is null)
                return (ContainerMatrix.InventoryPage1, slot);

        return null;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}

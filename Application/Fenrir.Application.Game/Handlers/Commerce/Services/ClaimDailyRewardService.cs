using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_CLAIM_REWARD_ITEM_SEND (opcode 155), extracted from <see cref="ClaimDailyRewardHandler" />.</summary>
public interface IClaimDailyRewardService
{
    /// <summary>
    ///     Resolves and applies today's daily-reward claim. Returns <c>null</c> when the caller should abort the
    ///     session as faulted; otherwise the response to send back to the client.
    /// </summary>
    ValueTask<ClaimDailyRewardResponse?> ResolveAndApplyAsync(ClaimDailyRewardRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);
}

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
            return new ClaimDailyRewardResponse
                { Result = 1, Value = new int[6], InvenPage = -1, InvenX = -1, InvenY = -1 };

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
            return new ClaimDailyRewardResponse
                { Result = 2, Value = new int[6], InvenPage = -1, InvenX = -1, InvenY = -1 };

        var newStack = new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
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

        var coupon = itemDefinition.Item.Sort == 99 ? 1 : 0;
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

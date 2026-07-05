using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Inventory;

/// <summary>Bridges the raw equipment container onto StatCalculator's input shape and back onto EffectiveStats.</summary>
public static class EquipmentService
{
    /// <summary>An ItemId no longer present in the catalog is skipped rather than thrown on.</summary>
    public static ImmutableArray<EquippedItemSlot> BuildEquippedSlots(
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer,
        FrozenDictionary<int, ItemDefinition> itemsById)
    {
        var builder = ImmutableArray.CreateBuilder<EquippedItemSlot>(equipmentContainer.Count);

        foreach (var (slot, stack) in equipmentContainer)
        {
            if (!itemsById.TryGetValue(stack.ItemId, out var definition))
                continue;

            builder.Add(new EquippedItemSlot(slot, definition.Item, stack.Enchant, stack.Combine, stack.Refine,
                stack.Socket));
        }

        return builder.ToImmutable();
    }

    /// <summary>Recomputes effective stats from the current Equipment container, buffs, and equipped pet.</summary>
    public static EffectiveStats RecomputeStats(
        CharacterBaseAttributes attributes,
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer,
        WorldDataCache worldData,
        BuffInfo? buffs = null,
        PetStatContribution pet = default)
    {
        var equipped = BuildEquippedSlots(equipmentContainer, worldData.ItemsById);
        return StatCalculator.ComputeEffectiveStats(attributes, equipped, worldData.LevelsByLevel, buffs, pet: pet);
    }
}

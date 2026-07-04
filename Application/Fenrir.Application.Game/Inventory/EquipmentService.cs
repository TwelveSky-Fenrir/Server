using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Inventory;

/// <summary>
///     Bridges the raw <see cref="ItemStack" /> equipment container onto <see cref="StatCalculator" />'s
///     input shape and back onto a fresh <see cref="EffectiveStats" /> snapshot. Consumes
///     <see cref="StatCalculator" />/<see cref="SetBonusTables" /> only -- never modifies them (mission
///     perimeter). Pure/static: takes <see cref="WorldDataCache" /> and plain values, never touches
///     <c>Zone</c> or <c>PlayerRuntimeState</c> directly, so both
///     <c>EnterWorldHandler</c> (world entry) and <c>GenericActionHandler</c> (a live
///     equip/unequip move) can call the exact same code path.
/// </summary>
public static class EquipmentService
{
    /// <summary>
    ///     Projects the Equipment container (slots 0-12) onto <see cref="StatCalculator" />'s
    ///     <see cref="EquippedItemSlot" /> list, resolving each occupied slot's ItemId against
    ///     <see cref="WorldDataCache.ItemsById" />. A slot whose ItemId is no longer in the catalog (a data
    ///     integrity gap, not a normal runtime condition) is silently skipped rather than thrown on -- the
    ///     same "absent contributes nothing" posture <see cref="StatCalculator" />'s own doc comment already
    ///     applies to every other unmodeled input.
    /// </summary>
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

    /// <summary>
    ///     Recomputes <c>PlayerRuntimeState.Stats</c> from a (possibly just-changed) Equipment
    ///     container snapshot. Called at world entry (<c>EnterWorldHandler</c>, no move involved --
    ///     just the persisted equipment), after every accepted equip/unequip move
    ///     (<c>GenericActionHandler</c>, on the PROJECTED post-move container -- see
    ///     <see cref="ContainerMatrix.ApplyMove" />), and by <c>Zone</c>'s own tick whenever the live buff
    ///     snapshot changes (a skill-cast buff applied, or <see cref="Fenrir.Application.Game.Simulation.BuffExpirySystem" />
    ///     expiring a slot -- Phase C/V3 Combat). <paramref name="buffs" /> defaults to null (no buffs applied,
    ///     matching <see cref="StatCalculator.ComputeEffectiveStats" />'s own "null = no buffs" default) so
    ///     every pre-V3 caller keeps compiling and behaving exactly as before.
    /// </summary>
    /// <param name="pet">
    ///     Server Logic V9 Progression: the equipped pet's Life/Mana/AttackPower/DefensePower contribution
    ///     (<see cref="Pets.PetGrowthCalculator" />) -- defaults to <c>default</c> (no pet) so every
    ///     pre-V9 caller keeps compiling and behaving exactly as before.
    /// </param>
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

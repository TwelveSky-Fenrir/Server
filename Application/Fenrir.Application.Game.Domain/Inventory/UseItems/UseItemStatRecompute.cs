using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     Pure equipment-derived stat recompute for the op23 families that change a stat-affecting field
///     (title/halo advancement) or the equipment container itself (double-click-to-equip). The same
///     assembly <c>UseInventoryItemService.RecomputeStatsAfterReset</c> and
///     <c>GenericActionService</c>'s equip path already perform — pet contribution folded in from the current
///     equip slot, buffs threaded through — kept here so the C9 handlers don't duplicate it. No I/O.
///     <para>
///         Legacy recomputes the whole stat block plus HP/MP after each of these mutations
///         (Server/ts25zone/S04_MyWork03.cpp:2430-2434 for equip-swap, :4614-4620 for title, :6966-6972 for
///         rank). Here that is the recomputed <see cref="EffectiveStats" /> (which carries the new MaxLife/
///         MaxMana) mirrored via a zone command; the "HP/MP" part is the new maxima, not a reset of current
///         Life/Mana to a floor.
///     </para>
/// </summary>
public static class UseItemStatRecompute
{
    /// <summary>Recomputes from the current equipment container with an overridden title/halo (for the 891/2193 families).</summary>
    public static EffectiveStats WithTitleHalo(PlayerRuntimeState state, WorldDataCache worldData, int title, int halo)
    {
        return Recompute(state, worldData, state.Inventory.GetContainer(ContainerMatrix.Equipment), title, halo);
    }

    /// <summary>Recomputes from a projected equipment container (for the double-click-to-equip swap), current title/halo.</summary>
    public static EffectiveStats WithEquipment(PlayerRuntimeState state, WorldDataCache worldData,
        ImmutableDictionary<byte, ItemStack> equipmentContainer)
    {
        return Recompute(state, worldData, equipmentContainer, state.Title, state.Halo);
    }

    private static EffectiveStats Recompute(PlayerRuntimeState state, WorldDataCache worldData,
        ImmutableDictionary<byte, ItemStack> equipmentContainer, int title, int halo)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, title, halo, state.RebirthCount, state.Level2);

        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        return EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution);
    }
}

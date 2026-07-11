using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public static class UseItemStatRecompute
{

        public static EffectiveStats WithTitleHalo(PlayerRuntimeState state, WorldDataCache worldData, int title, int halo)
    {
        return Recompute(state, worldData, state.Inventory.GetContainer(ContainerMatrix.Equipment), title, halo);
    }

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
            petContribution, runtimeState: state);
    }
}

using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.Stats;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    internal EffectiveStats RecomputeAndPublish(PlayerRuntimeState state, bool clampVitals = true)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount, state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);

        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, state);

        PublishStats(state, stats, clampVitals);
        return stats;
    }

    private void PublishStats(PlayerRuntimeState state, EffectiveStats stats, bool clampVitals)
    {
        state.Stats = stats;

        var changed = state.MaxLife != stats.MaxLife || state.MaxMana != stats.MaxMana;

        state.MaxLife = stats.MaxLife;
        state.MaxMana = stats.MaxMana;

        if (clampVitals && state.ClampVitalsToMax())
            changed = true;

        if (changed)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
    }
}

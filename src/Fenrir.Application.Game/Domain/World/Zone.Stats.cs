using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.Stats;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    internal EffectiveStats RecomputeAndPublish(PlayerRuntimeState state, bool clampVitals)
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

    internal void PublishStats(PlayerRuntimeState state, EffectiveStats stats, bool clampVitals)
    {
        state.Stats = stats;

        var changed = state.MaxLife != stats.MaxLife || state.MaxMana != stats.MaxMana;

        state.MaxLife = stats.MaxLife;
        state.MaxMana = stats.MaxMana;

        if (clampVitals)
        {
            if (state.Life > state.MaxLife)
            {
                state.Life = state.MaxLife;
                changed = true;
            }

            if (state.Mana > state.MaxMana)
            {
                state.Mana = state.MaxMana;
                changed = true;
            }
        }

        if (changed)
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
    }
}

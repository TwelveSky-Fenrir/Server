using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class PetActivitySystem(DirtyTracker<int> dirtyTracker, IPetLifecycleEffects? lifecycleEffects = null)
    : ISimulationSystem
{
    private const int PetActivityStatSort = 12;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (state.PetActivity < 1)
                continue;

            if (!state.Inventory.GetContainer(ContainerMatrix.Equipment)
                    .TryGetValue(PetSlots.EquipmentSlot, out var petStack) || petStack.ItemId == 0)
                continue;

            var transition = PetLifecycleTransitionResolver.ResolveDecay(state.PetGrowth, state.PetActivity,
                state.PetActivityDecayTicks, legacyTicksElapsed, SimulationClock.PetActivityDecayLegacyTicks,
                state.PetExpX2Time > 0);
            state.PetActivityDecayTicks = transition.NewDecayAccrualTicks;

            if (!transition.ActivityChanged)
                continue;

            state.PetActivity = transition.NewActivity;
            PetItemState.SynchronizeEquippedState(state.Inventory, transition.NewGrowth, transition.NewActivity);

            if (lifecycleEffects is { } effects)
            {
                effects.Apply(zone, state, in transition);
            }
            else
            {
                if (transition.RequiresStatRecalculation)
                    zone.RecomputeAndPublish(state);

                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = PetActivityStatSort, Value = transition.NewActivity, Value2 = 0 });
            }

            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }
    }
}

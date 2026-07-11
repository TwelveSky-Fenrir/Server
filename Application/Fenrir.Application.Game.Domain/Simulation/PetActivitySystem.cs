using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class PetActivitySystem(DirtyTracker<int> dirtyTracker) : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (state.PetActivity < 1)
                continue;

            if (!state.Inventory.GetContainer(ContainerMatrix.Equipment)
                    .TryGetValue(PetSlots.EquipmentSlot, out var petStack) || petStack.ItemId == 0)
                continue;

            state.PetActivityDecayTicks += legacyTicksElapsed;
            if (state.PetActivityDecayTicks < SimulationClock.PetActivityDecayLegacyTicks)
                continue;

            state.PetActivityDecayTicks -= SimulationClock.PetActivityDecayLegacyTicks;

            if (state.PetExpX2Time > 0)
                continue;

            state.PetActivity--;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }
    }
}

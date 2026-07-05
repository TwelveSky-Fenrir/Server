using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Pet activity decay (S07_MyGame04.cpp:835-861): -1 every 60 legacy ticks while a growable pet (iSort 22)
///     is equipped and activity hasn't reached 0. No-op if no pet, or the pet slot holds a Phoenix amulet
///     (iSort 28) instead -- see PetSlots.
/// </summary>
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
            state.PetActivity--;
            dirtyTracker.MarkDirty(state.CharacterId, DirtyFlags.Progression);
        }
    }
}

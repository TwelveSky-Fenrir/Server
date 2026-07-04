using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Pets;
using Fenrir.Application.Game.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     Pet activity decay (report 12 §2.1, verified <c>S07_MyGame04.cpp:835-861</c>): -1 every 60 legacy
///     ticks (<see cref="LegacyTime.PetActivityDecayLegacyTicks" />) while a growable pet (iSort 22) is
///     equipped and activity hasn't already reached 0. A no-op for anyone with no pet equipped, or whose
///     equipped pet-slot item isn't a growable pet at all (a Phoenix amulet, iSort 28) -- see
///     <see cref="PetSlots" />'s own remarks on the two mutually-exclusive occupants of that slot.
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
            if (state.PetActivityDecayTicks < LegacyTime.PetActivityDecayLegacyTicks)
                continue;

            state.PetActivityDecayTicks -= LegacyTime.PetActivityDecayLegacyTicks;
            state.PetActivity--;
            dirtyTracker.MarkDirty(state.CharacterId, DirtyFlags.Progression);
        }
    }
}

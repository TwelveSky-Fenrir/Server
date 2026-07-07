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
/// <remarks>
///     Entirely skipped -- the 60-tick cadence still resets, but the activity value itself is left untouched
///     -- while <see cref="PlayerRuntimeState.PetExpX2Time" /> ("Pet EXP boost pill") is above zero
///     (S07_MyGame04.cpp:854-860). See
///     <see cref="Fenrir.Application.Game.Domain.World.Zone.CreditPetGrowthFromMonsterKill" />
///     for that same counter's other effect (doubling pet-kill EXP).
/// </remarks>
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

            // Pet EXP boost pill active -- decay entirely paused this interval (see this type's own remarks).
            if (state.PetExpX2Time > 0)
                continue;

            state.PetActivity--;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }
    }
}

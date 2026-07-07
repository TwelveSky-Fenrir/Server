using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Once-per-real-minute countdown for the "Pet EXP boost pill" consumable's running duration counter
///     (<see cref="PlayerRuntimeState.PetExpX2Time" />) -- the same per-minute gate <see cref="PlayTimeAccrualSystem" />
///     uses for PlayTime1-3/Event and <see cref="SupportSkillTimeUpRatioMaintenanceSystem" /> uses for
///     BuffX2Time (S07_MyGame04.cpp:889 is the shared outer cadence variable all three systems' citations
///     fall under), kept as an independent system with its own accumulator for the same reason
///     <see cref="SupportSkillTimeUpRatioMaintenanceSystem" />'s own remarks give: sharing
///     <see cref="PlayerRuntimeState.PlayTimeAccrualTicks" /> here would double-consume/desync both cadences.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame04.cpp:889 (shared once-per-minute cadence variable, the same one
///     PlayTimeAccrualSystem's own citation covers) and :936-953 (counter countdown specifically at 941-953:
///     decremented by one only while an equipped pet item is found, and only while that pet's growth
///     percentage is still below 200% -- both gates independent, either one alone freezes the countdown).
///     <para>
///         Full-catch-up (integer-division) cadence, the same choice <see cref="PlayTimeAccrualSystem" />/
///         <see cref="SupportSkillTimeUpRatioMaintenanceSystem" /> made and for the same reason: a burst of N
///         accumulated legacy ticks (a stalled host catching up) must age
///         <see cref="PlayerRuntimeState.PetExpX2Time" /> down by N real minutes' worth in one pass, not just
///         one.
///     </para>
///     <para>
///         The "growth percentage still below 200%" gate is modeled here as
///         <c>PetGrowth &lt; PetGrowthCaps.Values[^1]</c> -- the shared cap array's own highest entry
///         (640,000,000), the one value this codebase already establishes as the designer-facing "200%
///         growth" ceiling (see <c>CreateAvatarService.StarterPetGrowth</c>'s own citation) and the only
///         growth cap any equipped pet item can ever reach (<see cref="PetExperienceCreditCalculator" />
///         already clamps growth at each item's own, always-lower-or-equal, category cap, so no pet's growth
///         can ever exceed this global maximum). The general <c>ReturnGrowPercent</c> formula itself is not
///         reproduced here -- same "out of scope" posture <see cref="PetGrowthTierCalculator" />'s own
///         remarks already document for that sibling routine; only the single 200% threshold this
///         consumable's own behavior contract calls for is checked.
///     </para>
/// </remarks>
public sealed class PetExpBoostCountdownSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(state, legacyTicksElapsed);
    }

    private static void TickPlayer(PlayerRuntimeState state, int legacyTicksElapsed)
    {
        state.PetExpX2TimeAccrualTicks += legacyTicksElapsed;
        var minutesElapsed = state.PetExpX2TimeAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
        if (minutesElapsed <= 0)
            return;

        state.PetExpX2TimeAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;

        if (state.PetExpX2Time < 1)
            return;

        // The countdown only ever evaluates once an equipped pet item is found -- independent of the
        // growth-percentage gate below (this consumable's own behavior contract, "no pet equipped" edge case).
        if (!state.Inventory.GetContainer(ContainerMatrix.Equipment)
                .TryGetValue(PetSlots.EquipmentSlot, out var petStack) || petStack.ItemId == 0)
            return;

        // "Growth percentage still below 200%" -- see this type's own remarks for why PetGrowthCaps.Values[^1]
        // is the faithful stand-in for the un-reproduced ReturnGrowPercent formula.
        if (state.PetGrowth >= PetGrowthCaps.Values[^1])
            return;

        state.PetExpX2Time = Math.Max(0, state.PetExpX2Time - minutesElapsed);
    }
}

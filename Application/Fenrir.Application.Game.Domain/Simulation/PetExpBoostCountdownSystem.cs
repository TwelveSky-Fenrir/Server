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
///         B8-pet-growth-depth correction: the "growth percentage still below 200%" gate now delegates to
///         <see cref="DoublePetExpTimerGate" />, which reproduces <c>PETSYSTEM::ReturnGrowPercent</c>'s real
///         per-item-category formula (via <c>StatCalculator.PetGrowPercent</c>) instead of the earlier
///         approximation this system used (<c>PetGrowth &gt;= PetGrowthCaps.Values[^1]</c>, the shared cap
///         array's global maximum) -- that approximation could only ever freeze the timer for a pet whose
///         own category cap equals the 640,000,000 global maximum, never for any lower-tier pet whose
///         growth is clamped well below it by <see cref="PetExperienceCreditCalculator" />. This also fixes a
///         second bug the approximation carried: an absent/unresolved pet slot now correctly keeps the timer
///         draining (the gate's own "lookup miss, not an explicit no-pet branch" edge case) instead of
///         freezing the countdown outright.
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

        // A missing/unresolved pet slot is a lookup miss, not an explicit "no pet" branch -- ItemId 0 falls
        // through DoublePetExpTimerGate's own table lookup the same way any other unrecognized id does, so
        // the countdown keeps draining unimpeded rather than freezing (contract edge case).
        var petItemId = state.Inventory.GetContainer(ContainerMatrix.Equipment)
            .TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        // Frozen at (or above) the 200% ceiling: left completely untouched for this tick, matching the
        // contract's own "drain normally below the ceiling, freeze completely at or above it" wording --
        // NOT decremented by even one minute of the elapsed catch-up burst.
        if (DoublePetExpTimerGate.IsAtFreezeThreshold(petItemId, state.PetGrowth))
            return;

        state.PetExpX2Time = Math.Max(0, state.PetExpX2Time - minutesElapsed);
    }
}

using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     <c>AdjustSymbolDamageInfo</c>'s damage-down half: recomputes every tribe's
///     <see cref="TribeSymbolCombatModifiers.GetDamageDownPenalty" /> from scratch every tick, with no memory
///     of the previous tick's value beyond what it overwrites -- a symbol changing hands mid-tick is reflected
///     immediately on the very next read, with no smoothing or grace period.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3144-3157 (function signature and tribe-0's opening
///     calculation, directly verified) ; :2663-2694 (per-tick invocation, unconditional on every zone instance,
///     immediately after the Tower system's own per-tick update and immediately before
///     <c>ProcessForGuardState</c> -- <see cref="TribeGuardCorridorStateDerivationSystem" /> in this codebase).
///     <para>
///         Registered as a genuine, unconditional <see cref="ISimulationSystem" /> -- unlike the multi-instance
///         RvR world-event schedulers elsewhere in this cluster (Holy Stone War, Regular War, Tribe Vote,
///         Alliance), which are deliberately driven by a dedicated Hosting-level background service instead of
///         the shared per-zone <see cref="ISimulationSystem" /> pipeline (see this cluster's own tick-budget
///         review), this recompute is legitimately cheap (four tribes, one boolean read and one comparison
///         each) AND the translated behavior contract confirms the legacy itself really does run this on
///         EVERY zone instance every tick, unconditionally -- so redundant, identical recomputation across
///         every zone this shard hosts is both harmless and faithful to the legacy's own per-process
///         architecture, not a wasted tick-budget concern the way a rarely-applicable multi-instance scheduler
///         would be.
///     </para>
///     <para>Only the damage-down half is modeled -- see <see cref="TribeSymbolCombatModifiers" />'s own GAP remarks.</para>
/// </remarks>
public sealed class TribeSymbolDamageModifierSystem(
    WorldStateService worldState,
    TribeSymbolCombatModifiers modifiers) : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
        {
            var holdsOwnSymbol = worldState.GetTribe(tribeId).HasSymbol;
            modifiers.SetDamageDownPenalty(tribeId,
                holdsOwnSymbol ? 0f : TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty);
        }
    }
}

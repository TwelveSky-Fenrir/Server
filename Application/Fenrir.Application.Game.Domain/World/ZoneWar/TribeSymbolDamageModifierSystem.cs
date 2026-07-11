using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     <c>AdjustSymbolDamageInfo</c>, both halves: recomputes every tribe's
///     <see cref="TribeSymbolCombatModifiers.GetDamageDownPenalty" /> (malus) AND, since wave15 (B15
///     contract), <see cref="TribeSymbolCombatModifiers.GetDamageUpBonusIncrementCount" /> (bonus increment
///     count) from scratch every tick, with no memory of the previous tick's value beyond what it overwrites --
///     a symbol changing hands mid-tick is reflected immediately on the very next read, with no smoothing or
///     grace period.
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
///         review), this recompute is legitimately cheap (four tribes, a handful of int/bool reads and
///         comparisons each) AND the translated behavior contract confirms the legacy itself really does run
///         this on EVERY zone instance every tick, unconditionally -- so redundant, identical recomputation
///         across every zone this shard hosts is both harmless and faithful to the legacy's own per-process
///         architecture, not a wasted tick-budget concern the way a rarely-applicable multi-instance scheduler
///         would be.
///     </para>
///     <para>
///         The bonus half's own gates, in order (B15 contract Side effects §1): (b) the whole computation is
///         zero unless this tribe (or its ally) currently owns its OWN slot; (c) +1 for each of the other three
///         slots this tribe (or its ally) also owns; (d) +1 more if this tribe (or its ally) also owns the
///         neutral monster/Yaoguai symbol; (e) if (c)+(d) produced nothing, +1 more (and ONLY +1, never
///         combined with (c)/(d)) if this tribe is the strict, single lowest-combined-points tribe among tribes
///         whose own combined (self + ally) points are at least
///         <see cref="TribeSymbolCombatModifiers.SmallTribeAdvantagePointFloor" /> -- ties, or this tribe itself
///         being under the floor, both yield no fallback increment. Only the increment COUNT is computed here;
///         see <see cref="TribeSymbolCombatModifiers" />'s own remarks for why the per-increment damage
///         magnitude is deliberately left unmodeled.
///     </para>
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

            modifiers.SetDamageUpBonusIncrementCount(tribeId, ComputeDamageUpBonusIncrementCount(tribeId));
        }
    }

    private int ComputeDamageUpBonusIncrementCount(byte tribeId)
    {
        var ally = worldState.GetAllyOf(tribeId);

        // Gate (b): the whole computation, INCLUDING the small-tribe fallback in (e), is zero unless this
        // tribe (or its ally) currently owns its own slot.
        var ownsOwnSlot = IsControlledByTribeOrAlly(worldState.GetTribeSymbolOwner(tribeId), tribeId, ally);
        if (!ownsOwnSlot)
            return 0;

        var count = 0;

        // (c): up to three increments, one per OTHER tribe's slot this tribe (or its ally) also controls.
        for (byte otherSlot = 0; otherSlot < WorldStateService.TribeCount; otherSlot++)
        {
            if (otherSlot == tribeId)
                continue;
            if (IsControlledByTribeOrAlly(worldState.GetTribeSymbolOwner(otherSlot), tribeId, ally))
                count++;
        }

        // (d): one further increment for the neutral monster/Yaoguai symbol, if likewise controlled.
        if (worldState.World.MonsterSymbol is { } monsterSymbolOwner &&
            IsControlledByTribeOrAlly(monsterSymbolOwner, tribeId, ally))
            count++;

        if (count > 0)
            return count;

        // (e): small-tribe-advantage fallback -- only reachable when (c)+(d) produced nothing.
        return IsStrictlyLowestEligibleTribeByCombinedPoints(tribeId) ? 1 : 0;
    }

    private static bool IsControlledByTribeOrAlly(byte ownerTribeId, byte tribeId, byte? ally)
    {
        return ownerTribeId == tribeId || (ally is { } allyId && ownerTribeId == allyId);
    }

    /// <summary>
    ///     Each tribe's OWN ally (not the queried <paramref name="tribeId" />'s) is what determines that other
    ///     tribe's own combined total -- re-derived per tribe in the loop below, not the caller's ally.
    /// </summary>
    private bool IsStrictlyLowestEligibleTribeByCombinedPoints(byte tribeId)
    {
        var combined = new int[WorldStateService.TribeCount];
        for (byte i = 0; i < WorldStateService.TribeCount; i++)
        {
            var points = worldState.GetTribe(i).Points;
            if (worldState.GetAllyOf(i) is { } allyOfI)
                points += worldState.GetTribe(allyOfI).Points;
            combined[i] = points;
        }

        var ownCombined = combined[tribeId];
        if (ownCombined < TribeSymbolCombatModifiers.SmallTribeAdvantagePointFloor)
            return false;

        for (byte i = 0; i < WorldStateService.TribeCount; i++)
        {
            if (i == tribeId)
                continue;
            if (combined[i] < TribeSymbolCombatModifiers.SmallTribeAdvantagePointFloor)
                continue; // not eligible -- excluded from the comparison entirely, per (e)'s own floor
            if (combined[i] <= ownCombined)
                return false; // tied or strictly lower rival among the eligible tribes -- no fallback
        }

        return true;
    }
}

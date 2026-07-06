namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Shared ceiling-check math for every "banked counter" consumable family in this document (loot-box LOD
///     round counter, protection/enhancement charges, stat potions, cash-timer counters). Two distinct
///     ceiling-check styles are modeled side by side rather than normalized to one, because the legacy itself
///     is inconsistent here and that inconsistency is a documented, deliberately-preserved finding, not a bug
///     to fix during the port.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/function.h:132-140 (<c>CheckOverMaximum</c>, explicit 64-bit-safe ceiling
///     check, used by the charm sub-group of the protection/enhancement family and by the LOD round counter) ;
///     Server/ts25zone/S04_MyWork03.cpp:106-117 (<c>wCheckAdd</c>/<c>wCheckAdd2</c> macros, plain 32-bit
///     addition-then-compare, used by the scroll sub-group and most of the cash-timer family) ;
///     Server/Header/Protocol/DEFINE.h:365 (2,000,000,000 shared ceiling).
/// </remarks>
public static class BankedCounterMath
{
    /// <summary>The shared 2,000,000,000 ceiling nearly every banked counter in this document is checked against.</summary>
    public const int GlobalCeiling = 2_000_000_000;

    public enum AddOutcome
    {
        Success,

        /// <summary>Adding would exceed the ceiling -- clean failure, counter left untouched.</summary>
        WouldExceedCeiling
    }

    /// <summary>
    ///     <c>CheckOverMaximum</c> -- widens to 64-bit before comparing, so a bulk add of up to 999 units can
    ///     never silently wrap a 32-bit counter before the ceiling comparison happens. Use for every charm
    ///     sub-group counter and the LOD round counter.
    /// </summary>
    public static AddResult AddWideSafe(int current, long amount, int ceiling = GlobalCeiling)
    {
        var projected = (long)current + amount;
        return projected > ceiling
            ? new AddResult(AddOutcome.WouldExceedCeiling, current)
            : new AddResult(AddOutcome.Success, (int)projected);
    }

    /// <summary>
    ///     <c>wCheckAdd</c>/<c>wCheckAdd2</c> -- plain 32-bit addition-then-compare, the style the scroll
    ///     sub-group and most of the cash-timer family use instead of <see cref="AddWideSafe" />. Never
    ///     realistically exploitable given the small fixed amounts these ids ever add (1-3, or 60-180), but
    ///     kept distinct from <see cref="AddWideSafe" /> on purpose -- see this type's own remarks.
    /// </summary>
    public static AddResult AddNarrow(int current, int amount, int ceiling = GlobalCeiling)
    {
        unchecked
        {
            var projected = current + amount;
            return projected > ceiling
                ? new AddResult(AddOutcome.WouldExceedCeiling, current)
                : new AddResult(AddOutcome.Success, projected);
        }
    }

    /// <summary>
    ///     Headroom-based bulk coercion for a tiered/capped counter (the stat-potion family): reduces the
    ///     per-use bulk count so <paramref name="current" /> + <paramref name="perUnitAmount" /> * count never
    ///     exceeds <paramref name="cap" />. Returns 0 (not a rejection -- the caller distinguishes "already
    ///     maxed" itself) when there is no headroom left at all.
    /// </summary>
    public static int CoerceBulkToHeadroom(int current, int cap, int perUnitAmount, int requestedCount)
    {
        if (perUnitAmount <= 0 || requestedCount <= 0)
            return 0;

        var headroom = cap - current;
        if (headroom <= 0)
            return 0;

        var maxUnits = headroom / perUnitAmount;
        return Math.Min(requestedCount, maxUnits);
    }

    public readonly record struct AddResult(AddOutcome Outcome, int NewValue)
    {
        public bool Succeeded => Outcome == AddOutcome.Success;
    }
}

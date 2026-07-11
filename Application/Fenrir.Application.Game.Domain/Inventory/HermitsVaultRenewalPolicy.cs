using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     C1-vault-expiry-enforcement, trigger 3: the pure date-arithmetic core of a Hermits Vault
///     (<c>aInventoryDate</c>) renewal -- deliberately NOT wired into <c>UseInventoryItemService</c> yet. See
///     this type's own remarks for why.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:4045-4107 -- the renewal switch for both the Storage-Vault
///     (<c>aStoreDate</c>, out of scope here) and Hermits-Vault (<c>aInventoryDate</c>) item families: seven
///     qualifying item indices (807, 1102, 1129, 1141, 2019, 1356, 8407), each adding a fixed day count of
///     7, 30, 60, or 180 depending on which item, plus the ordered success side effects (date write, a
///     cash-item-usage log entry, one-unit quantity decrement) ; Server/Header/datetime.h:195-218 -- the
///     underlying date-addition routine (<see cref="GameDate.TryAddDays" />), confirming the
///     future-expiry-extends-forward baseline rule this type implements and the failure return used when the
///     day count is negative or the internal date re-derivation fails.
///     <para>
///         <b>Deliberately unwired:</b> the originating contract confirms the seven item ids and states each
///         adds "7/30/60/180 depending on which of the seven," but does not itself supply the id-to-day-count
///         mapping (which of the seven ids maps to which of the four counts) -- inventing that mapping would
///         violate this workstream's "never invent a magnitude" rule. <see cref="QualifyingItemIds" /> is
///         populated so the exact catalog is ready the moment that mapping is supplied by a future contract;
///         <see cref="TryComputeRenewedExpiry" /> needs only an already-resolved day count and has no
///         per-item knowledge at all, so wiring the mapping in later is a pure catalog addition, not a change
///         to this policy.
///     </para>
///     <para>
///         The baseline's "still in the future relative to today" test is not restated with an explicit
///         comparator in the originating contract's own side-effect text, so this reuses the SAME
///         greater-than-or-equal-is-still-valid convention every other <c>aInventoryDate</c> gate in this
///         workstream applies to the identical field (see <c>UseInventoryItemService.ResolveAsync</c>'s own
///         gate) -- a consistency inference, not an invented threshold.
///     </para>
/// </remarks>
public static class HermitsVaultRenewalPolicy
{
    /// <summary>
    ///     The seven Hermits Vault renewal item ids confirmed by this policy's own citation. Day-count mapping
    ///     is NOT included -- see this type's own remarks.
    /// </summary>
    public static readonly FrozenSet<int> QualifyingItemIds =
        new[] { 807, 1102, 1129, 1141, 2019, 1356, 8407 }.ToFrozenSet();

    /// <summary>
    ///     Computes the renewed expiry date for an already-resolved <paramref name="dayCount" /> (the caller
    ///     must already know which of the seven qualifying items was consumed and how many days it grants --
    ///     see this type's own remarks for why that mapping is not modeled here).
    ///     <para>
    ///         Baseline selection: when <paramref name="currentExpiry" /> is still valid (&gt;=
    ///         <paramref name="today" />), the baseline is <paramref name="currentExpiry" /> itself -- renewal
    ///         stacks forward from the existing future expiry. When <paramref name="currentExpiry" /> has
    ///         already lapsed (or is the zero sentinel), the baseline is simply <paramref name="today" />.
    ///     </para>
    /// </summary>
    /// <returns>
    ///     <see langword="false" /> when <paramref name="dayCount" /> is negative or the underlying calendar
    ///     projection cannot be represented (<see cref="GameDate.TryAddDays" /> failure) -- the caller must
    ///     leave the character's expiry and inventory completely untouched in that case, per the originating
    ///     contract's own side effect 4 (all four success side effects are one all-or-nothing unit).
    /// </returns>
    public static bool TryComputeRenewedExpiry(int currentExpiry, int today, int dayCount, out int newExpiry)
    {
        if (dayCount < 0)
        {
            newExpiry = GameDate.Invalid;
            return false;
        }

        var baseline = currentExpiry >= today ? currentExpiry : today;
        return GameDate.TryAddDays(baseline, dayCount, out newExpiry);
    }
}

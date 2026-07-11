using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     The two id whitelists op23's costume/stellar-core intermediate dispatch block routes on
///     (<c>IsValidCostume</c>/<c>IsValidStellarCore</c>, Server/Header/function.h:1798-2008/:2267-2303) --
///     confirmed disjoint by direct read of both tables (workstream C9-costume-stellar-whitelist contract).
/// </summary>
/// <remarks>
///     <para>
///         <b>Costume table reused, not redefined.</b> <see cref="StatCalculator.ValidCostumeIds" /> (workstream
///         B6, <c>Fenrir.Application.Game.Stats.StatCalculator.CostumeContribution.cs</c>) is already a
///         byte-identical transcription of <c>IsValidCostume</c>'s 195-id live table, built for the
///         stat-contribution surface. Reused verbatim here rather than re-transcribed a second time --
///         <c>Fenrir.Application.Game.Domain</c> already references <c>Fenrir.Application.Game.Stats</c>, so
///         there is no layering reason to duplicate it.
///     </para>
///     <para>
///         <b>Stellar-core table has no equivalent public name to reuse.</b>
///         <c>StatCalculator.StellarCoreContribution.cs</c> tables the same 28-id key range (tier-1
///         76527-76540, tier-2 93500-93513) across four <c>private</c> dictionaries
///         (<c>StellarDamageDefenseBonusById</c>/<c>StellarElementBonusById</c> both key on the full 28;
///         <c>StellarCriticalDefenceBonusById</c>/<c>StellarMaxLifeBonusById</c> key on a narrower subset) --
///         none of those are a public "is this id a valid stellar core" set, and they are scoped to
///         stat-contribution math, not item-use dispatch validity. <see cref="ValidStellarCoreIds" /> below is
///         therefore its own small, independently-built 28-id <see cref="FrozenSet{T}" /> over the same two
///         ranges, cross-verified against (not derived from) that file's own citation to the same
///         function.h:2267-2303 lines.
///     </para>
/// </remarks>
public static class CostumeStellarCoreWhitelist
{
    /// <summary>Tier-1 band start (<c>IsValidStellarCore</c>, function.h:2267-2303).</summary>
    private const int Tier1Start = 76527;

    private const int Tier1End = 76540;
    private const int Tier2Start = 93500;
    private const int Tier2End = 93513;

    /// <summary>
    ///     The 28-id <c>IsValidStellarCore</c> table: tier-1 76527-76540 (14 ids) + tier-2 93500-93513 (14
    ///     ids). Cross-verified disjoint from <see cref="StatCalculator.ValidCostumeIds" /> per the originating
    ///     contract's own Edge cases (both tables read start-to-finish).
    /// </summary>
    public static readonly FrozenSet<int> ValidStellarCoreIds = BuildValidStellarCoreIds();

    private static FrozenSet<int> BuildValidStellarCoreIds()
    {
        var ids = new HashSet<int>();
        for (var id = Tier1Start; id <= Tier1End; id++)
            ids.Add(id);
        for (var id = Tier2Start; id <= Tier2End; id++)
            ids.Add(id);
        return ids.ToFrozenSet();
    }

    /// <summary>True when <paramref name="itemId" /> is in the live <c>IsValidCostume</c> table.</summary>
    public static bool IsValidCostume(int itemId)
    {
        return StatCalculator.ValidCostumeIds.Contains(itemId);
    }

    /// <summary>True when <paramref name="itemId" /> is in the live <c>IsValidStellarCore</c> table.</summary>
    public static bool IsValidStellarCore(int itemId)
    {
        return ValidStellarCoreIds.Contains(itemId);
    }

    /// <summary>
    ///     True when either whitelist claims <paramref name="itemId" /> -- the registry routing predicate. Check
    ///     order does not matter for routing purposes (the two tables are disjoint), but
    ///     <see cref="CostumeStellarCoreUseItemHandler" /> itself still evaluates costume before stellar-core to
    ///     match the legacy call-site order (<c>IsValidCostume</c> before <c>IsValidStellarCore</c>,
    ///     S04_MyWork03.cpp:2489 then :2528).
    /// </summary>
    public static bool ClaimsItem(int itemId)
    {
        return IsValidCostume(itemId) || IsValidStellarCore(itemId);
    }
}

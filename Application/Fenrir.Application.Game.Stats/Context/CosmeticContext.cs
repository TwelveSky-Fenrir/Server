using System.Collections.Immutable;

namespace Fenrir.Application.Game.Stats.Context;

/// <summary>
///     Cosmetic BASE-layer stat inputs the legacy <c>MyFactor</c> base recompute reads but the current
///     equipment-only <see cref="StatCalculator" /> surface has no channel for: the rune system, the worn
///     costume, and the stellar core. Every field defaults to a neutral/zero value, so a <c>default</c>
///     instance reproduces "no cosmetic contribution" -- exactly the calculator's behaviour today. All inputs
///     here feed the BASE cache (recompute-on-discrete-event), never the per-resolution effective layer.
/// </summary>
/// <remarks>
///     Introduced by workstream B1 as a pure plumbing seam: the fields are threaded through
///     <see cref="StatCalculator.ComputeBaseStats" /> and its base getters but are not read by any formula yet
///     (each still contributes 0). Later workstreams (rune/costume/stellar) consume them without changing any
///     signature. Réf. legacy input surface: rune <c>Server/Header/Protocol/MyFactor.cpp:1685-1695</c>
///     (aRuneSystem/aRuneSystemStat lookup inside GetBaseVitality, representative of all four base primary-stat
///     functions); costume + costume-enchant-V2 <c>MyFactor.cpp:1678-1684</c> (aCostumeValue /
///     aCostumeEnchantValue); stellar core <c>MyFactor.cpp:2692,2949,3602,3801</c> (aStellarCoreNumber via the
///     stellar lookup tables in base attack/defense/crit-defense/elemental attack).
/// </remarks>
/// <param name="RuneItemIds">
///     aRuneSystem[4] -- the socketed rune item id per rune slot (index-aligned), 0 = empty. Assembled from
///     <c>PlayerRuntimeState.RuneSystem</c>. A <c>default</c>/empty array means "no runes"; consumers must
///     guard with <see cref="ImmutableArray{T}.IsDefaultOrEmpty" /> (the default of a
///     <see cref="ImmutableArray{T}" /> is uninitialised, not an empty enumerable).
/// </param>
/// <param name="RuneStatValues">
///     aRuneSystemStat[4] -- the socketed rune's packed enchant/combine/refine/socket int, paired 1:1 with
///     <paramref name="RuneItemIds" />. Same default/empty convention.
/// </param>
/// <param name="CostumeNumber">
///     aCostumeNumber -- the currently-worn costume id (0 = none). The raw id only; the derived
///     <c>aCostumeValue</c>/<c>aCostumeEnchantValue</c> stat blocks are resolved from a costume catalog by the
///     consuming workstream, not carried here.
/// </param>
/// <param name="CostumeState">aCostumeState -- the costume visibility toggle (0/1).</param>
/// <param name="StellarCoreNumber">aStellarCoreNumber -- the currently-worn stellar core id (0 = none).</param>
/// <param name="CostumeValue">
///     aCostumeValue -- the worn costume's cached four base stats (Vitality/Strength/Intelligence/Dexterity),
///     already deco-clamped and/or valid-costume-bonused (see
///     <see cref="StatCalculator.ComputeCostumeBaseStatBlock" />). Resolved from a <c>world.Items</c> catalog
///     lookup on <see cref="CostumeNumber" /> at the assembly point (<see cref="StatCalculator" /> itself holds
///     no item catalog), introduced by workstream B6. A <c>default</c> (all-zero) block means "no costume
///     resolved" -- same convention as <see cref="RuneItemIds" />'s empty-array default.
/// </param>
/// <param name="CostumeEnchantCs">
///     The decoded costume-enchant magnitude <c>cs</c> (the one live field of <c>aCostumeEnchantValue</c> in the
///     shipped V2 build -- see <see cref="StatCalculator.DecodeCostumeEnchantCs" />). Introduced by workstream
///     B6 as a pre-decoded value rather than carrying the raw <c>aCostumeIndex</c>/<c>aCostumeDate</c> pair,
///     since <see cref="StatCalculator" /> holds no per-character state of its own. The assembly point
///     (<c>EquipmentService.AssembleStatContexts</c>) now decodes this from
///     <c>PlayerRuntimeState.CostumeIndex</c>/<c>PlayerRuntimeState.CostumeDate</c>; it is 0 (no enchant bonus)
///     whenever the costume isn't in the worn index range (10-19) -- the same value a fresh/unworn character
///     already produced before <c>CostumeDate</c> existed, so this remains a pure strengthening, not a
///     behavior change for anyone not actively wearing an enchanted costume.
/// </param>
public readonly record struct CosmeticContext(
    ImmutableArray<int> RuneItemIds = default,
    ImmutableArray<int> RuneStatValues = default,
    int CostumeNumber = 0,
    int CostumeState = 0,
    int StellarCoreNumber = 0,
    CostumeBaseStatBlock CostumeValue = default,
    int CostumeEnchantCs = 0);

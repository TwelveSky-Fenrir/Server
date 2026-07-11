using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Enchant;

/// <summary>
///     The wing-specific (target slot-type 6, <c>iSort</c>==<c>IWING</c>) material whitelist for
///     <c>CZ_IMPROVE_ITEM_SEND</c> (workstream C12-warlord-chest, "part C") -- a two-gate check: Gate 1 is a
///     class-level whitelist (<c>CheckWingEnchantMaterial</c>), Gate 2 is a separate amount-lookup switch
///     keyed by the same material item id. Passing Gate 1 is necessary but NOT sufficient; the two are not
///     kept in sync in the legacy source itself (see <see cref="Gate2MissingDisconnects" />).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:3016-3025 (dispatch by slot category, <c>iSort</c>==6 for
///     wing) ; Server/Header/function.h:2578-2599 (<c>CheckWingEnchantMaterial</c>, Gate 1, verified
///     verbatim: 695, 696, 698, 826, 2387, 2392, 2397 unconditionally, plus 8106 gated on the always-live
///     <c>LNW33</c> macro and 99409 gated on the never-live <c>ONLINE_FOR_DS</c> macro --
///     Server/ts25latest_config.props:4-12, Server/Header/Protocol/DEFINE.h:21-51) ;
///     Server/ts25zone/S04_MyWork02.cpp:3036-3079 (Gate 2, the amount-lookup switch: confirmed a case exists
///     for 826 -- amount 50, USE_IMPROVE_RATE_100-gated but that macro is unconditionally defined at :19 --
///     and confirmed NO case exists for 2387 or 2392) ; :3076-3078 (the switch's <c>default:</c> case, which
///     disconnects the session) ; :3222-3306 (per-material success/failure behavior once a legal material is
///     identified: 826's forced-success helper, 8106's dedicated early-exit-on-failure path with its own ZC
///     result code 9, immune to level-loss/destroy) ; Server/Header/function.h:2553-2576
///     (<c>CheckItemEnchantMaterial</c>, the PARALLEL non-wing whitelist, cited only for contrast -- it is a
///     genuinely different id set from this one, NOT the same table
///     <see cref="EnchantMaterialCatalog.StandardMaterials" />/<see cref="EnchantMaterialCatalog.AdvancedMaterials" />
///     already models).
///     <para>
///         <b>Corrects an assumption in <see cref="EnchantResolver" />'s own remarks</b> (written before this
///         workstream's contract existed): that type states wings are "resolved by the SAME
///         ResolveStandard/ResolveAdvanced machinery... materials... are all shared" with every other
///         equipment slot in the band. That is now known to be inaccurate for the MATERIAL WHITELIST
///         specifically -- wings use this distinct Gate-1/Gate-2 id set, not
///         <see cref="EnchantMaterialCatalog.StandardMaterials" />/<see cref="EnchantMaterialCatalog.AdvancedMaterials" />
///         .
///         The +0..+40/+41..+50 regime split, the destroy-probability formula, and
///         <see cref="EnchantResolver.SafeImproveValue" />=20 remain correctly shared between wings and
///         every other equipment slot -- only the legal-MATERIAL set differs. This type is data only; wiring
///         <see cref="EnchantResolver" />/<see cref="EnchantMaterialCatalog" /> to actually branch on
///         <see cref="EnchantResolver.EnchantResult.IsWing" /> and consult THIS whitelist instead of the
///         shared tables is deferred to a later pass (both files are outside this workstream's edit
///         permission) -- see this workstream's wiring manifest.
///     </para>
/// </remarks>
public static class WingEnchantMaterialWhitelist
{
    /// <summary>Item 826: forced-success wing scroll. Gate-2 amount 50 (S04_MyWork02.cpp:3047).</summary>
    public const int GuaranteedSuccessScrollItemId = 826;

    /// <summary>Item 826's Gate-2 amount -- moot in practice since its success probability is separately forced to 100.</summary>
    public const int GuaranteedSuccessScrollAmount = 50;

    /// <summary>
    ///     Item 8106: the wing Protection material -- live in every real build (<c>LNW33</c>-gated, always
    ///     defined). On a FAILED enchant attempt it takes a dedicated early-exit (own ZC result code, see
    ///     <see cref="ProtectedMaterialFailureResultCode" />): the material is consumed, no level-decrease or
    ///     destroy occurs -- immune to loss on failure, the wing analogue of the non-wing 8101 material's own
    ///     <see cref="EnchantMaterialCatalog.StandardMaterial.NoChangeOnFailure" /> shape, but with its own
    ///     distinct wire result code since the caller distinguishes wing vs. non-wing targets
    ///     (S04_MyWork02.cpp:3264). Its own Gate-2 SUCCESS increment amount is not cited by this workstream's
    ///     contract -- do not invent one; see <see cref="WhitelistedAmountNotCited" />'s own remarks for the
    ///     same posture (8106 is intentionally NOT included in that set only because its FAILURE behavior,
    ///     unlike 695/696/698/2397, IS fully specified -- only its success amount is open).
    /// </summary>
    public const int ProtectedMaterialItemId = 8106;

    /// <summary>
    ///     ZC result code for a FAILED 8106 attempt (S04_MyWork02.cpp:3264) -- distinct from the non-wing
    ///     8101 material's own code 8 (<see cref="EnchantResolver.EnchantOutcome.NoChange" />'s own remarks),
    ///     since the caller maps the "no-change" result differently for wing vs. non-wing targets.
    /// </summary>
    public const int ProtectedMaterialFailureResultCode = 9;

    /// <summary>
    ///     Gate 1 (the class whitelist). 99409 is textually present in source but gated behind
    ///     <c>ONLINE_FOR_DS</c>, dead in every real build -- deliberately excluded here, never a legal
    ///     material in production.
    /// </summary>
    public static readonly FrozenSet<int> ClassWhitelist =
        new[] { 695, 696, 698, 826, 2387, 2392, 2397, 8106 }.ToFrozenSet();

    /// <summary>
    ///     Whitelisted at Gate 1 but CONFIRMED to have no matching case in the Gate-2 amount-lookup switch --
    ///     an inconsistent legacy whitelist entry with no discoverable rationale in the cited source (no
    ///     comment, no adjacent history). Presenting either as a wing-enchant material passes Gate 1, then
    ///     falls into the switch's default case, which disconnects the session.
    /// </summary>
    public static readonly FrozenSet<int> Gate2MissingDisconnects = new[] { 2387, 2392 }.ToFrozenSet();

    /// <summary>
    ///     Whitelisted at Gate 1, with Gate 2 presumed (by elimination -- this contract explicitly flags only
    ///     2387/2392 as Gate-2-absent, not these) to have a matching case, but this workstream's own contract
    ///     did not cite the specific per-material increment amount for any of these three. Do NOT invent a
    ///     magnitude here -- flag for a follow-up legacy-behavior-translator pass citing
    ///     S04_MyWork02.cpp:3036-3079's individual case values for these ids specifically.
    /// </summary>
    public static readonly FrozenSet<int> WhitelistedAmountNotCited = new[] { 695, 696, 698, 2397 }.ToFrozenSet();
}

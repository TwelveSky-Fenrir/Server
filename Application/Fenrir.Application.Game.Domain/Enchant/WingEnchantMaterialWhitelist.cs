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
///         every other equipment slot -- only the legal-MATERIAL set differs. Wiring
///         <see cref="EnchantResolver" />/<see cref="EnchantMaterialCatalog" /> to fully branch on
///         <see cref="EnchantResolver.EnchantResult.IsWing" /> and consult THIS whitelist for every material
///         instead of the shared tables remains a later pass for the still-unresolved ids (696/698/826/
///         2387/2392/2397) -- but the "enchant-resolver-wing-8106-ticket" follow-up contract below supplied
///         <see cref="ProtectedMaterialItemId" />'s (8106) own missing magnitude, and
///         <see cref="EnchantResolver" /> now wires that one material's full behavior (see that type's own
///         remarks and its <c>ResolveWingProtectedMaterial</c>).
///     </para>
///     <para>
///         <b>2026-07-11 supplemental finding (enchant-resolver-wing-8106-ticket)</b>, independently
///         re-verified: Server/ts25zone/S04_MyWork02.cpp:3051-3056 -- the per-attempt enchant VALUE for
///         material 8106 (gated on the always-live <c>LNW33</c>, per the citations above) and its sibling
///         695 (compiled unconditionally, same case label, NOT <c>LNW33</c>-gated) is the same flat +1
///         (<see cref="ProtectedMaterialEnchantValue" />). :3084-3091 confirms the Wing-category cost is
///         keyed by the EQUIPPED ITEM'S category, not by material -- contrasted against the material-priced
///         default path at :3092-3099 -- and :3222-3237 shows that flat cost (<see cref="WingEnchantCpCost" />
///         = 50, contribution points, not money) is debited unconditionally before the success roll is drawn,
///         win or lose. 695's own FAILURE path is a DIFFERENT code block than 8106's (the one cited at
///         :3259-3267 names 8106 specifically) and was not observed in the cited range of that finding --
///         do NOT assume it shares 8106's NoChange/immune-to-loss shape; <see cref="EnchantResolver" />
///         therefore only wires 8106, not 695, leaving 695 in <see cref="ClassWhitelist" /> (Gate 1 passes)
///         but out of any modeled success/failure path (Rejected until a follow-up contract resolves its
///         failure semantics).
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
    ///     (S04_MyWork02.cpp:3264). Its per-attempt SUCCESS increment amount and cost are now resolved --
    ///     see <see cref="ProtectedMaterialEnchantValue" />/<see cref="WingEnchantCpCost" /> and this type's
    ///     own 2026-07-11 supplemental-finding remarks -- and fully wired by
    ///     <see cref="EnchantResolver" />'s <c>ResolveWingProtectedMaterial</c>.
    /// </summary>
    public const int ProtectedMaterialItemId = 8106;

    /// <summary>
    ///     ZC result code for a FAILED 8106 attempt (S04_MyWork02.cpp:3264) -- distinct from the non-wing
    ///     8101 material's own code 8 (<see cref="EnchantResolver.EnchantOutcome.NoChange" />'s own remarks),
    ///     since the caller maps the "no-change" result differently for wing vs. non-wing targets.
    /// </summary>
    public const int ProtectedMaterialFailureResultCode = 9;

    /// <summary>
    ///     The flat per-attempt enchant increment shared by material 8106 and its sibling 695
    ///     (Server/ts25zone/S04_MyWork02.cpp:3051-3056, same case label) -- see this type's own 2026-07-11
    ///     supplemental-finding remarks. <see cref="EnchantResolver" /> only wires this for 8106 itself
    ///     (695's failure path is unresolved -- see <see cref="SiblingWithSharedEnchantValueItemId" />).
    /// </summary>
    public const int ProtectedMaterialEnchantValue = 1;

    /// <summary>
    ///     Item 695: shares material 8106's exact +1 enchant-value assignment (same case label, but compiled
    ///     unconditionally -- NOT <c>LNW33</c>-gated) per Server/ts25zone/S04_MyWork02.cpp:3051-3056. Its own
    ///     FAILURE path is a genuinely different code block than 8106's (the dedicated early-exit cited at
    ///     :3259-3267 names 8106 specifically) and was never observed by the finding that recovered the
    ///     shared +1 value -- do NOT assume it shares 8106's NoChange/immune-to-loss shape; it may instead
    ///     fall through to the ordinary destroy-risk path that follows that block. Flagged for a
    ///     <c>cpp-zone-gameplay-analyst</c> re-check before modeling 695 at all;
    ///     <see cref="EnchantResolver" /> deliberately leaves 695 unmodeled (Rejected) pending that follow-up.
    /// </summary>
    public const int SiblingWithSharedEnchantValueItemId = 695;

    /// <summary>
    ///     The flat Wing-CATEGORY enchant cost (contribution points, never money) debited unconditionally
    ///     before the success roll is drawn, win or lose -- keyed by the EQUIPPED item's category, not by
    ///     material (Server/ts25zone/S04_MyWork02.cpp:3084-3091, contrasted against the material-priced
    ///     default path at :3092-3099; the debit itself at :3222-3237). Conceptually applies to every wing
    ///     material, but <see cref="EnchantResolver" /> currently only wires it for the fully-specified 8106
    ///     case (see this type's own 2026-07-11 supplemental-finding remarks) -- the caller
    ///     (<c>EnchantItemService</c>) routes <see cref="EnchantResolver.EnchantResult.Cost" /> to CP instead
    ///     of money via <see cref="EnchantResolver.EnchantResult.IsWing" />, so this constant slots into that
    ///     same field unchanged.
    /// </summary>
    public const int WingEnchantCpCost = 50;

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
    ///     S04_MyWork02.cpp:3036-3079's individual case values for these ids specifically. 695 is deliberately
    ///     NOT in this set any more: the 2026-07-11 supplemental finding cited its enchant-VALUE assignment
    ///     (<see cref="SiblingWithSharedEnchantValueItemId" />'s own remarks) -- what remains open for 695 is
    ///     its failure-path behavior, a different concern than "amount not cited".
    /// </summary>
    public static readonly FrozenSet<int> WhitelistedAmountNotCited = new[] { 696, 698, 2397 }.ToFrozenSet();
}

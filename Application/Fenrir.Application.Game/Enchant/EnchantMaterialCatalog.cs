namespace Fenrir.Application.Game.Enchant;

/// <summary>
///     Static material tables for CZ_IMPROVE_ITEM_SEND's STANDARD equipment regime (report
///     04_mega_switches.md §4, verified against <c>Server/ts25zone/S04_MyWork02.cpp:2833-3450</c>). Wings
///     (<c>eq.iSort == 6</c>, CP-costed, ailes) and the costume/stellar-core branches are OUT of scope --
///     see <see cref="EnchantResolver" />'s own remarks.
/// </summary>
/// <remarks>
///     Every value here was read directly off the two verified switch statements, NOT off report 04's own
///     prose summary: <c>USE_IMPROVE_RATE_100</c> is unconditionally <c>#define</c>'d at the top of
///     <c>S04_MyWork02.cpp</c> (line 19, NOT gated by any build config) so ALL of these materials are live in
///     every build, including the "150%/108%/90%/120%/3%" scrolls report 04 lists as conditional.
/// </remarks>
public static class EnchantMaterialCatalog
{
    public enum TypeRequirement : byte
    {
        None,
        RareOnly,
        EliteOnly,
        RareOrElite
    }

    /// <summary>The ONLY material that advances +40 -&gt; +41 (no roll, always succeeds, cost 0).</summary>
    public const int UnsealItemId = 1422;

    /// <summary>+0..+40 regime materials (<c>CheckItemEnchantMaterial</c>, function.h:2553-2576).</summary>
    public static readonly IReadOnlyDictionary<int, StandardMaterial> StandardMaterials =
        new Dictionary<int, StandardMaterial>
        {
            [1019] = new(1019, 1, 10000, false, false, TypeRequirement.None, null, false),
            [1020] = new(1020, 2, 30000, false, false, TypeRequirement.None, null, false),
            [1021] = new(1021, 3, 50000, false, false, TypeRequirement.None, null, false),
            [1022] = new(1022, 4, 70000, false, false, TypeRequirement.None, null, false),
            [1023] = new(1023, 5, 90000, false, false, TypeRequirement.None, null, false),
            // LNW33 alternate id for the SAME +1 stone, at 1000x the money cost (verified, not a typo).
            [8101] = new(8101, 1, 10000000, false, false, TypeRequirement.None, null, false),
            // IsEEnchantScroll (function.h:2756-2770): these 7 all force p1=100 -- the destroy roll is
            // therefore UNREACHABLE for any of them (only the 6 stone materials above ever roll a destroy).
            [633] = new(633, 1, 0, false, true, TypeRequirement.None, null, false),
            [619] = new(619, 40, 0, true, true, TypeRequirement.RareOrElite, null, false),
            [540] = new(540, 30, 0, true, true, TypeRequirement.RareOnly, 30, false),
            [538] = new(538, 36, 0, true, true, TypeRequirement.EliteOnly, 36, false),
            [551] = new(551, 36, 0, true, true, TypeRequirement.RareOnly, 36, false),
            [565] = new(565, 36, 0, true, true, TypeRequirement.RareOnly, 36, false),
            [825] = new(825, 50, 0, true, true, TypeRequirement.RareOrElite, null, true)
        };

    /// <summary>+41..+50 regime materials. Item 1422 (the +40-&gt;+41 "UNSEAL" step) is handled separately -- it never rolls.</summary>
    public static readonly IReadOnlyDictionary<int, AdvancedMaterial> AdvancedMaterials =
        new Dictionary<int, AdvancedMaterial>
        {
            [1023] = new(1023, 1, 90000, false),
            [1243] = new(1243, 2, 110000, false),
            [1437] = new(1437, 2, 110000, false),
            [1457] = new(1457, 2, 110000, false),
            [633] = new(633, 1, 0, true),
            [825] = new(825, 10, 0, true)
        };

    /// <summary>
    ///     One material's rule for the +0..+40 regime. <see cref="IsFillToValue" />: <see cref="Value" /> is
    ///     the TARGET absolute enchant level (<c>iValue = Value - currentImprove</c>) rather than a flat
    ///     increment. <see cref="IgnoresFortyCap" /> (material 825 ONLY): the only material allowed to jump
    ///     straight past +40 in a single +0..+40-regime attempt -- verified real, not a guess (S04_MyWork02.cpp
    ///     :3178-3184, "if (mat.iInfo->iIndex != 825)" guards the generic 40-clamp).
    /// </summary>
    public readonly record struct StandardMaterial(
        int ItemId,
        int Value,
        int MoneyCost,
        bool IsFillToValue,
        bool ForcesGuaranteedSuccess,
        TypeRequirement RequiredType,
        int? MaxCurrentImproveExclusive,
        bool IgnoresFortyCap);

    /// <summary>One material's rule for the +41..+50 regime (<c>MAX_IMPROVE_150</c>, S04_MyWork02.cpp:2886-2912).</summary>
    public readonly record struct AdvancedMaterial(int ItemId, int Value, int MoneyCost, bool ForcesGuaranteedSuccess);
}

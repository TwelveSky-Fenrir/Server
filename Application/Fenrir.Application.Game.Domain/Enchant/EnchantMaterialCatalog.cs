namespace Fenrir.Application.Game.Domain.Enchant;

/// <summary>
///     Static material tables for CZ_IMPROVE_ITEM_SEND's standard equipment regime. Wings and the
///     costume/stellar-core branches are out of scope -- see <see cref="EnchantResolver" />'s remarks.
/// </summary>
/// <remarks>
///     <c>USE_IMPROVE_RATE_100</c> is unconditionally defined, so all materials here are live in every build (not
///     conditional as elsewhere assumed).
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

    /// <summary>The only material that advances +40 -&gt; +41 (no roll, always succeeds, cost 0).</summary>
    public const int UnsealItemId = 1422;

    public static readonly IReadOnlyDictionary<int, StandardMaterial> StandardMaterials =
        new Dictionary<int, StandardMaterial>
        {
            [1019] = new(1019, 1, 10000, false, false, TypeRequirement.None, null, false),
            [1020] = new(1020, 2, 30000, false, false, TypeRequirement.None, null, false),
            [1021] = new(1021, 3, 50000, false, false, TypeRequirement.None, null, false),
            [1022] = new(1022, 4, 70000, false, false, TypeRequirement.None, null, false),
            [1023] = new(1023, 5, 90000, false, false, TypeRequirement.None, null, false),
            // Alternate id for the same +1 stone, at 1000x the money cost (verified, not a typo).
            [8101] = new(8101, 1, 10000000, false, false, TypeRequirement.None, null, false),
            // These 7 all force p1=100 -- the destroy roll is unreachable for any of them.
            [633] = new(633, 1, 0, false, true, TypeRequirement.None, null, false),
            [619] = new(619, 40, 0, true, true, TypeRequirement.RareOrElite, null, false),
            [540] = new(540, 30, 0, true, true, TypeRequirement.RareOnly, 30, false),
            [538] = new(538, 36, 0, true, true, TypeRequirement.EliteOnly, 36, false),
            [551] = new(551, 36, 0, true, true, TypeRequirement.RareOnly, 36, false),
            [565] = new(565, 36, 0, true, true, TypeRequirement.RareOnly, 36, false),
            [825] = new(825, 50, 0, true, true, TypeRequirement.RareOrElite, null, true)
        };

    /// <summary>Item 1422 (the +40-&gt;+41 "unseal" step) is handled separately -- it never rolls.</summary>
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
    ///     <see cref="IsFillToValue" />: <see cref="Value" /> is a target absolute level, not a flat increment.
    ///     <see cref="IgnoresFortyCap" /> (material 825 only): the sole material allowed to jump straight past +40.
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

    public readonly record struct AdvancedMaterial(int ItemId, int Value, int MoneyCost, bool ForcesGuaranteedSuccess);
}

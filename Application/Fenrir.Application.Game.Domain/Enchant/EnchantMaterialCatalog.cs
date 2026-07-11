namespace Fenrir.Application.Game.Domain.Enchant;

public static class EnchantMaterialCatalog
{
    public enum TypeRequirement : byte
    {
        None,
        RareOnly,
        EliteOnly,
        RareOrElite
    }

        public const int UnsealItemId = 1422;

    public static readonly IReadOnlyDictionary<int, StandardMaterial> StandardMaterials =
        new Dictionary<int, StandardMaterial>
        {
            [1019] = new(1019, 1, 10000, false, false, TypeRequirement.None, null, false),
            [1020] = new(1020, 2, 30000, false, false, TypeRequirement.None, null, false),
            [1021] = new(1021, 3, 50000, false, false, TypeRequirement.None, null, false),
            [1022] = new(1022, 4, 70000, false, false, TypeRequirement.None, null, false),
            [1023] = new(1023, 5, 90000, false, false, TypeRequirement.None, null, false),
            [8101] = new(8101, 1, 10000000, false, false, TypeRequirement.None, null, false, true),
            [633] = new(633, 1, 0, false, true, TypeRequirement.None, null, false),
            [619] = new(619, 40, 0, true, true, TypeRequirement.RareOrElite, null, false),
            [540] = new(540, 30, 0, true, true, TypeRequirement.RareOnly, 30, false),
            [538] = new(538, 36, 0, true, true, TypeRequirement.EliteOnly, 36, false),
            [551] = new(551, 36, 0, true, true, TypeRequirement.RareOnly, 36, false),
            [565] = new(565, 36, 0, true, true, TypeRequirement.RareOnly, 36, false),
            [825] = new(825, 50, 0, true, true, TypeRequirement.RareOrElite, null, true)
        };

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

        public readonly record struct StandardMaterial(
        int ItemId,
        int Value,
        int MoneyCost,
        bool IsFillToValue,
        bool ForcesGuaranteedSuccess,
        TypeRequirement RequiredType,
        int? MaxCurrentImproveExclusive,
        bool IgnoresFortyCap,
        bool NoChangeOnFailure = false);

    public readonly record struct AdvancedMaterial(int ItemId, int Value, int MoneyCost, bool ForcesGuaranteedSuccess);
}

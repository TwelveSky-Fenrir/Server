namespace Fenrir.Application.Game.Domain.Crafting;

public static class CraftRecipeCatalog
{
    public const int JadeUpgradeSort = 1;

    public const int AdvancedElixirSort = 4;

    public const int PurpleJadeItemId = 1024;
    public const int RedJadeItemId = 1025;

    public const int AdvancedElixirRequiredQuantity = 10;

    public const int AdvancedElixirSuccessRatePercent = 20;

    public const int AdvancedElixirResultBaseItemId = 801;

    public const int AdvancedElixirResultRange = 6;


    public const int StoneMatCombineSort = 0;

    public const int StoneMatMaterialItemId = 1019;


    public const int MountFusionTier1Sort = 2;
    public const int MountFusionTier2Sort = 3;

    public const int MountFusionTier1CatalystItemId = 92286;
    public const int MountFusionTier2CatalystItemId = 92287;

    public const int MountFusionTier1SuccessNumerator = 20;

    public const int MountFusionTier1SuccessDenominator = 100;

    public const int MountFusionTier2SuccessNumerator = 30;

    public const int MountFusionTier2SuccessDenominator = 500;

    public const int MountFusionTier1FailureDustQuantity = 3;
    public const int MountFusionTier2FailureDustQuantity = 9;


    public const int DustItemId = 92291;

    public const int WingFeatherWhiteItemId = 695;
    public const int WingFeatherBlackItemId = 696;
    public const int WingFeatherGoldItemId = 698;
    public const int WingFeatherBlessingItemId = 2397;


    public const int WingAssemblySort = 40;

    public const int WingAssemblyContributionPointCost = 50;


    public const int FeatherTierUpSort = 41;


    public const int WingTierRerollSort = 42;
    public const int WingTierRerollCatalystItemId = 2477;
    public const int WingTierRerollFailureDustQuantity = 3;


    public const int WingFourthTierSort = 44;
    public const int WingFifthTierSort = 45;

    public const int WingFourthMaterialItemId = 1407;
    public const int WingFourthCatalystItemId = 92289;
    public const int WingFourthResultItemId = 1401;
    public const int WingFourthFailureDustQuantity = 3;

    public const int WingFifthMaterialItemId = 1401;
    public const int WingFifthCatalystItemId = 92290;
    public const int WingFifthResultItemId = 1403;
    public const int WingFifthFailureDustQuantity = 15;


    public const int DustRecycleWingSort = 80;
    public const int DustRecycleCloakSort = 81;
    public const int DustRecycleAnimalSort = 82;
    public const int DustRecyclePet1Sort = 83;
    public const int DustRecyclePet2Sort = 84;

    public const int DustRecycleWingThreshold = 15;
    public const int DustRecycleCloakThreshold = 45;
    public const int DustRecycleAnimalThreshold = 15;
    public const int DustRecyclePet1Threshold = 15;
    public const int DustRecyclePet2Threshold = 300;

    public const int DustRecycleCloakResultItemId = 1401;

    public static readonly IReadOnlySet<int> AdvancedElixirBaseItemIds =
        new HashSet<int> { 506, 507, 508, 578, 579, 509 };

    public static readonly IReadOnlyList<int> StoneMatResultPool = [1020, 1021, 1022, 1023];

    public static readonly IReadOnlySet<int> MountFusionTier1MaterialItemIds =
        new HashSet<int> { 1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326 };

    public static readonly IReadOnlySet<int> MountFusionTier2MaterialItemIds =
        new HashSet<int> { 1304, 1305, 1306, 1314, 1318, 1321, 1324, 1327 };

    public static readonly IReadOnlyList<int> MountFusionTier1ResultPool =
        [1304, 1305, 1306, 1314, 1318, 1321, 1324, 1327];

    public static readonly IReadOnlyList<int> MountFusionTier2ResultPool =
        [1307, 1308, 1309, 1315, 1319, 1322, 1325, 1328];

    private static readonly IReadOnlyList<int> WingTierBaseItemIds = [201, 204, 207, 210, 213];

    public static readonly IReadOnlySet<short> WingAssemblyTownMapIds = new HashSet<short> { 1, 6, 11, 37, 140 };

    public static readonly IReadOnlySet<int> WingAssemblyCatalystItemIds =
        new HashSet<int> { 126, 129, 132, 135, 138, 141, 144, 147, 150 };

    public static int WingTierItemId(int tier, byte previousTribe)
    {
        return WingTierBaseItemIds[tier - 1] + previousTribe;
    }
}

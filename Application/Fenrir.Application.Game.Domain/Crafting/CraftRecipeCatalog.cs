namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>
///     Constants for the CZ_MAKE_ITEM_SEND (op29) recipes this covers: the original two (Jade upgrade, Advanced
///     Elixir) plus the reachable <c>MK_*</c> family verified against the shipped <c>#ifdef LNW33</c>/<c>USE_WING</c>
///     branch (stone-mat combine, mount fusion, wing assembly/tier-up/reroll, wing-dust recycling). See
///     <see cref="CraftResolver" />'s remarks for the ones still out of scope.
/// </summary>
public static class CraftRecipeCatalog
{
    public const int JadeUpgradeSort = 1;

    public const int AdvancedElixirSort = 4;

    public const int PurpleJadeItemId = 1024;
    public const int RedJadeItemId = 1025;

    public const int AdvancedElixirRequiredQuantity = 10;

    /// <summary>Verified real rate -- the legacy's own source comment claims 5%, but the code rolls 20.</summary>
    public const int AdvancedElixirSuccessRatePercent = 20;

    public const int AdvancedElixirResultBaseItemId = 801;

    public const int AdvancedElixirResultRange = 6;

    // -- MK_MATS_01019 (sort 0) -- "stone-mat combine". Server/ts25zone/S04_MyWork02.cpp:4353-4403. --

    public const int StoneMatCombineSort = 0;

    public const int StoneMatMaterialItemId = 1019;

    // -- MK_ANIMAL_NUM_1 / MK_ANIMAL_NUM_2 (sorts 2/3) -- "mount fusion". :4541-4640. --

    public const int MountFusionTier1Sort = 2;
    public const int MountFusionTier2Sort = 3;

    public const int MountFusionTier1CatalystItemId = 92286;
    public const int MountFusionTier2CatalystItemId = 92287;

    /// <summary>tRandom = rand_mir() % 100; success if tRandom &lt; 20 -- :4586-4589.</summary>
    public const int MountFusionTier1SuccessNumerator = 20;

    public const int MountFusionTier1SuccessDenominator = 100;

    /// <summary>tRandom = rand_mir() % 500; success if tRandom &lt; 30 -- :4591-4594.</summary>
    public const int MountFusionTier2SuccessNumerator = 30;

    public const int MountFusionTier2SuccessDenominator = 500;

    public const int MountFusionTier1FailureDustQuantity = 3;
    public const int MountFusionTier2FailureDustQuantity = 9;

    // -- Wing family shared items. --

    /// <summary>"Wing dust" -- the universal consolation/recycling currency of the whole USE_WING region.</summary>
    public const int DustItemId = 92291;

    public const int WingFeatherWhiteItemId = 695;
    public const int WingFeatherBlackItemId = 696;
    public const int WingFeatherGoldItemId = 698;

    // -- MK_WING_0 (sort 40) -- "wing assembly". :5096-5269. --

    public const int WingAssemblySort = 40;

    /// <summary>
    ///     tCostCP -- aKillOtherTribe, i.e. Fenrir's <c>PlayerRuntimeState.ContributionPoints</c> (confirmed via
    ///     mUTIL.ProcessForCP / B_AVATAR_CHANGE_INFO_2 S003CONTRIBUTION_POINT at S07_MyGame03.cpp:149-157).
    ///     Deducted before the roll regardless of outcome -- :5140-5202.
    /// </summary>
    public const int WingAssemblyContributionPointCost = 50;

    // -- MK_WING_1 / MK_WING_3 (sorts 41/43) -- "feather tier-up chain". :5270-5340. --

    public const int FeatherTierUpWhiteToBlackSort = 41;
    public const int FeatherTierUpBlackToGoldSort = 43;
    public const int FeatherTierUpRequiredQuantity = 10;

    // -- MK_WING_2 (sort 42) -- "wing tier reroll". :5342-5404. --

    public const int WingTierRerollSort = 42;
    public const int WingTierRerollCatalystItemId = 2477;
    public const int WingTierRerollFailureDustQuantity = 3;

    // -- MK_WING_5 / MK_WING_6 (sorts 45/46). :5480-5551. --

    public const int WingFifthTierSort = 45;

    /// <summary>
    ///     MK_WING_6 shares MK_WING_5's outer case but its own inner-switch branch is commented out
    ///     (:5505-5517) -- not compiled in the shipped build. A request with this sort still passes the
    ///     shared CheckInv(1..4) bounds-only gate (no item-identity/quantity check runs for it at all) and
    ///     unconditionally lands on the "no item produced" dust-consolation branch. This is a legacy
    ///     validation bypass, not a deliberate design -- flagged for fenrir-security-hardening-engineer /
    ///     cpp-security-debt-auditor before shipping; implemented byte-for-byte here (legacy parity) rather
    ///     than unilaterally hardened.
    /// </summary>
    public const int WingSixthTierUnvalidatedSort = 46;

    public const int WingFifthMaterialItemId = 1401;
    public const int WingFifthCatalystItemId = 92290;
    public const int WingFifthResultItemId = 1403;
    public const int WingFifthFailureDustQuantity = 15;

    // -- MK_DUST_WING/CLOAK/ANIMAL/PET1/PET2 (sorts 80-84) -- "wing-dust recycling". :5553-5858. --

    public const int DustRecycleWingSort = 80;
    public const int DustRecycleCloakSort = 81;
    public const int DustRecycleAnimalSort = 82;
    public const int DustRecyclePet1Sort = 83;
    public const int DustRecyclePet2Sort = 84;

    public const int DustRecycleWingThreshold = 15;
    public const int DustRecycleCloakThreshold = 45;
    public const int DustRecycleAnimalThreshold = 45;
    public const int DustRecyclePet1Threshold = 15;
    public const int DustRecyclePet2Threshold = 300;

    public const int DustRecycleCloakResultItemId = 1401;

    public static readonly IReadOnlySet<int> AdvancedElixirBaseItemIds =
        new HashSet<int> { 506, 507, 508, 578, 579, 509 };

    /// <summary>randomStoneMat() -- Server/ts25zone/S04_MyWork03.cpp:70-75, uniform over these 4.</summary>
    public static readonly IReadOnlyList<int> StoneMatResultPool = [1020, 1021, 1022, 1023];

    /// <summary>
    ///     IsValidMount5 -- Server/Header/function.h:2305-2320 / DEFINE.h:157-178
    ///     (Tiger/Pig/Deer/Bear/Cat/Bull/Wolf/Lion, "1" grade).
    /// </summary>
    public static readonly IReadOnlySet<int> MountFusionTier1MaterialItemIds =
        new HashSet<int> { 1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326 };

    /// <summary>IsValidMount10 -- same header, "2" grade.</summary>
    public static readonly IReadOnlySet<int> MountFusionTier2MaterialItemIds =
        new HashSet<int> { 1304, 1305, 1306, 1314, 1318, 1321, 1324, 1327 };

    /// <summary>animalArray1[0] -- :4341, "2" grade pool (same set as <see cref="MountFusionTier2MaterialItemIds" />).</summary>
    public static readonly IReadOnlyList<int> MountFusionTier1ResultPool =
        [1304, 1305, 1306, 1314, 1318, 1321, 1324, 1327];

    /// <summary>animalArray1[1] -- :4342, "3" grade pool.</summary>
    public static readonly IReadOnlyList<int> MountFusionTier2ResultPool =
        [1307, 1308, 1309, 1315, 1319, 1322, 1325, 1328];

    /// <summary>
    ///     tWingV1..tWingV5 = 201/204/207/210/213 + aPreviousTribe -- :4332-4337. Index 0 = tier 1 (lowest,
    ///     the common roll outcome), index 4 = tier 5 (top, the rare roll outcome). Tier 6 (tWingV6 = 216 +
    ///     previousTribe) has no compiled producer in this build (MK_WING_6's own branch is dead code -- see
    ///     <see cref="WingSixthTierUnvalidatedSort" />'s remarks) so it is deliberately not modeled here.
    /// </summary>
    private static readonly IReadOnlyList<int> WingTierBaseItemIds = [201, 204, 207, 210, 213];

    /// <summary>IsValidTownAll -- Server/Header/mapcheck.h:116-128, unconditional (not PPSHOP_V2-gated).</summary>
    public static readonly IReadOnlySet<short> WingAssemblyTownMapIds = new HashSet<short> { 1, 6, 11, 37, 140 };

    public static readonly IReadOnlySet<int> WingAssemblyCatalystItemIds =
        new HashSet<int> { 126, 129, 132, 135, 138, 141, 144, 147, 150 };

    /// <param name="tier">1-5, see <see cref="WingTierBaseItemIds" />.</param>
    public static int WingTierItemId(int tier, byte previousTribe)
    {
        return WingTierBaseItemIds[tier - 1] + previousTribe;
    }

    // MK_DUST_PET1's weighted pool (tRandom % 25: <4=1006, <8=1007, <12=1008, <16=1009, <20=1010, else=1011
    // -- :5735-5748) and MK_DUST_PET2's (tRandom % 15: <1=1016, <3=1012, <6=1013, <9=1014, else=1015 --
    // :5801-5812) are unequal-weight, not a uniform pick over a list, so they're expressed directly as
    // CraftResolver.PickDustPet1/PickDustPet2's own roll-to-item switch rather than duplicated here.
}

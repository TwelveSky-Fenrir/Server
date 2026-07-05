namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>Constants for the 6 CZ_MAKE_PET_SEND (op88) recipes -- S04_MyWork02.cpp:12125-12501, LNW33+__GOD__ build.</summary>
public static class PetCraftRecipeCatalog
{
    public const int Recipe1Sort = 0;
    public const int Recipe2Sort = 1;
    public const int Recipe3Sort = 2;
    public const int Recipe4Sort = 3;
    public const int Recipe5Sort = 4;
    public const int Recipe6Sort = 5;
    public const int Recipe1CatalystItemId = 1178;

    public const int Recipe2CatalystItemId = 1179;

    /// <summary>Both fusion recipes' "miss" consolation item -- quantity differs per recipe (3 vs 15).</summary>
    public const int ConsolationItemId = 92291;

    public const int Recipe1ConsolationQuantity = 3;
    public const int Recipe2ConsolationQuantity = 15;

    public const int Recipe3Material1ItemId = 1013;
    public const int Recipe3Material2ItemId = 1014;
    public const int Recipe3Material3ItemId = 1015;
    public const int Recipe3CatalystItemId = 1235;
    public const int Recipe3ResultItemId = 1012;

    public const int Recipe4MaterialItemId = 1012;
    public const int Recipe4ResultItemId = 1016;

    public const int Recipe5Material1ItemId = 1012;
    public const int Recipe5Material2ItemId = 1016;

    public const int Recipe6Material1ItemId = 1012;
    public const int Recipe6Material2ItemId = 2160;

    /// <summary>PetGrowLess(1, x)/PetGrowLess(2, x) -- gates recipes 5/6 only (__GOD__ build).</summary>
    public const int GodRecipeMaterial1GrowthThreshold = 320_000_000;

    public const int GodRecipeMaterial2GrowthThreshold = 640_000_000;

    /// <summary>wMakePet(160000000) -- the produced pet's fresh growth value for recipes 5/6 only.</summary>
    public const int GodRecipeResultGrowthValue = 160_000_000;

    public static readonly IReadOnlySet<int> Recipe1FusionItemIds = new HashSet<int> { 1002, 1003, 1004, 1005 };
    public static readonly IReadOnlyList<int> Recipe1ResultPool = [1006, 1007, 1008, 1009, 1010, 1011];

    public static readonly IReadOnlySet<int> Recipe2FusionItemIds =
        new HashSet<int> { 1006, 1007, 1008, 1009, 1010, 1011 };

    public static readonly IReadOnlyList<int> Recipe5ResultPool = [1310, 1311, 1312];
    public static readonly IReadOnlyList<int> Recipe6ResultPool = [2133, 2144];
}

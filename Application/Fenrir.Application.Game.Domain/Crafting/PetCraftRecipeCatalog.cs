namespace Fenrir.Application.Game.Domain.Crafting;

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

    public const int GodRecipeMaterial1GrowthThreshold = 320_000_000;

    public const int GodRecipeMaterial2GrowthThreshold = 640_000_000;

    public const int GodRecipeResultGrowthValue = 160_000_000;

    public static readonly IReadOnlySet<int> Recipe1FusionItemIds = new HashSet<int> { 1002, 1003, 1004, 1005 };
    public static readonly IReadOnlyList<int> Recipe1ResultPool = [1006, 1007, 1008, 1009, 1010, 1011];

    public static readonly IReadOnlySet<int> Recipe2FusionItemIds =
        new HashSet<int> { 1006, 1007, 1008, 1009, 1010, 1011 };

    public static readonly IReadOnlyList<int> Recipe5ResultPool = [1310, 1311, 1312];
    public static readonly IReadOnlyList<int> Recipe6ResultPool = [2133, 2144];
}

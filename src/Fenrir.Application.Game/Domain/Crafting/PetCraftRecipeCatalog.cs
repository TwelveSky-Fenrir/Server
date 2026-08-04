namespace Fenrir.Application.Game.Domain.Crafting;

public static class PetCraftRecipeCatalog
{
    public const int Recipe0Sort = 0;
    public const int Recipe1Sort = 1;
    public const int Recipe2Sort = 2;
    public const int Recipe3Sort = 3;

    public const int Recipe0CatalystItemId = 1178;
    public const int Recipe1CatalystItemId = 1179;
    public const int FallbackItemId = 92291;

    public const int Recipe0FallbackQuantity = 3;
    public const int Recipe1FallbackQuantity = 15;

    public const int Recipe2Material1ItemId = 1013;
    public const int Recipe2Material2ItemId = 1014;
    public const int Recipe2Material3ItemId = 1015;
    public const int Recipe2CatalystItemId = 1235;
    public const int Recipe2ResultItemId = 1012;

    public const int Recipe3MaterialItemId = 1012;
    public const int Recipe3ResultItemId = 1016;

    public static readonly IReadOnlySet<int> Recipe0FusionItemIds = new HashSet<int> { 1002, 1003, 1004, 1005 };
    public static readonly IReadOnlyList<int> Recipe0ResultPool = [1006, 1007, 1008, 1009, 1010, 1011];

    public static readonly IReadOnlySet<int> Recipe1FusionItemIds =
        new HashSet<int> { 1006, 1007, 1008, 1009, 1010, 1011 };
}

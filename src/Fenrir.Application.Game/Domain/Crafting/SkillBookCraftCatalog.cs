namespace Fenrir.Application.Game.Domain.Crafting;

public static class SkillBookCraftCatalog
{
    public const int Recipe1Sort = 0;
    public const int Recipe2Sort = 1;
    public const int Recipe3Sort = 2;

    public static readonly (int Material1, int Material2, int Material3, int Material4, int ResultItemId)[] Recipes =
    [
        (1054, 1055, 1056, 1057, 90567),
        (1058, 1059, 1060, 1061, 90568),
        (1062, 1063, 1064, 1065, 90569)
    ];
}

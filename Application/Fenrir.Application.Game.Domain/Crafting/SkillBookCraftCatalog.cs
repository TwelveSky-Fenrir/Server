namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>Constants for CZ_MAKE_SKILL_SEND (op30) -- S04_MyWork02.cpp:5868-6017.</summary>
public static class SkillBookCraftCatalog
{
    public const int Recipe1Sort = 0;
    public const int Recipe2Sort = 1;
    public const int Recipe3Sort = 2;
    public const int WarGodSort = 3;

    public const int WarGodMaterial1ItemId = 99301;
    public const int WarGodMaterial2ItemId = 99302;
    public const int WarGodMaterial3ItemId = 99303;
    public const int WarGodMaterial4ItemId = 99304;

    /// <summary>The 3 unconditional fragment-fusion recipes, indexed by wire Sort (0-2).</summary>
    public static readonly (int Material1, int Material2, int Material3, int Material4, int ResultItemId)[] Recipes =
    [
        (1054, 1055, 1056, 1057, 90567),
        (1058, 1059, 1060, 1061, 90568),
        (1062, 1063, 1064, 1065, 90569)
    ];

    /// <summary>MyUtil::ReturnWarGodSkill, LNW33 branch (S07_MyGame03.cpp:5694-5709) -- indexed [tribe][column].</summary>
    public static readonly int[][] WarGodSkillsByTribe =
    [
        [90787, 90786, 90788],
        [90789, 90790, 90791],
        [90793, 90792, 90794]
    ];
}

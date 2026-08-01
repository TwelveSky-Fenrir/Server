namespace Fenrir.Application.Game.Domain.Crafting;

public static class LegendaryPetCraftCatalog
{
    public const int Sort = 2;

    public const byte Material1RequiredSort1 = 31;

    public const byte Material1RequiredSort2 = 32;

    public const int ContributionPointCost = 10000;

    public const int WireResult = 21;

    public static readonly IReadOnlySet<int> CatalystItemIds = new HashSet<int> { 1878, 2150 };

    public static readonly IReadOnlyList<int> LegendaryPool1 = [1839, 1840, 1841, 1842, 1889, 1890, 17204];
    public static readonly IReadOnlyList<int> LegendaryPool2 = [1838, 1887, 1888, 17202, 17203];

    public static readonly IReadOnlyList<int> GuardianPool =
        [17335, 17336, 17337, 17338, 17339, 17340, 17341, 17342, 17343, 17344, 17345, 17346];
}

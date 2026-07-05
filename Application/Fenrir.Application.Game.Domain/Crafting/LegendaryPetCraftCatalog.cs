namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>
///     Constants for CZ_MAKE_ITEM2_SEND (op131) tSort==2 -- the only reachable case, an early
///     <c>
///         if (tSort != 2)
///         Quit()
///     </c>
///     guard makes tSort 0/1/3 dead code (S04_MyWork02.cpp:14902-14906, case 2 at :15074-15141).
/// </summary>
public static class LegendaryPetCraftCatalog
{
    public const int Sort = 2;

    /// <summary>Material1 (the pet being upgraded) must already be a Legendary-tier pet: world.Items.Sort 31 or 32.</summary>
    public const byte Material1RequiredSort1 = 31;

    public const byte Material1RequiredSort2 = 32;

    public const int ContributionPointCost = 10000;

    /// <summary>B_MAKE_ITEM2_RECV's tResult is unconditionally 21 here (USE_MATS_999 active), not a 0/1 success flag.</summary>
    public const int WireResult = 21;

    public static readonly IReadOnlySet<int> CatalystItemIds = new HashSet<int> { 1878, 2150 };

    public static readonly IReadOnlyList<int> LegendaryPool1 = [1839, 1840, 1841, 1842, 1889, 1890, 17204];
    public static readonly IReadOnlyList<int> LegendaryPool2 = [1838, 1887, 1888, 17202, 17203];

    public static readonly IReadOnlyList<int> GuardianPool =
        [17335, 17336, 17337, 17338, 17339, 17340, 17341, 17342, 17343, 17344, 17345, 17346];
}

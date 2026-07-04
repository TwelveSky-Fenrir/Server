namespace Fenrir.Application.Game.Crafting;

/// <summary>
///     Constants for the two CZ_MAKE_ITEM_SEND recipes this pass implements -- other <c>MK_*</c> families are out of
///     scope, see <see cref="CraftResolver" />'s remarks.
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

    public static readonly IReadOnlySet<int> AdvancedElixirBaseItemIds =
        new HashSet<int> { 506, 507, 508, 578, 579, 509 };
}

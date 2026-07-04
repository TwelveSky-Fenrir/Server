namespace Fenrir.Application.Game.Crafting;

/// <summary>
///     Constants for the two CZ_MAKE_ITEM_SEND (opcode 29, MK_* dispatch, report 04_mega_switches.md §3)
///     recipes this pass implements -- <see cref="JadeUpgradeSort" /> (MK_MATS_01024, a deterministic
///     guaranteed conversion) and <see cref="AdvancedElixirSort" /> (MK_ELIXIR_NEW, the probabilistic
///     "most common craft family" the mission brief calls for), both verified against
///     <c>Server/ts25zone/S04_MyWork02.cpp:4256-4467</c>. Every OTHER <c>MK_*</c> family (random-weighted
///     stone tables, mount/wing fusion, dust recycling...) is explicitly OUT of scope -- see
///     <see cref="CraftResolver" />'s own remarks.
/// </summary>
public static class CraftRecipeCatalog
{
    /// <summary><c>make_item.cfg.h</c>'s <c>MK_MATS_01024</c> -- active in EVERY build (not gated by LNW33).</summary>
    public const int JadeUpgradeSort = 1;

    /// <summary>
    ///     <c>make_item.cfg.h</c>'s <c>MK_ELIXIR_NEW</c> -- LNW33-only value (verified active for the EU33 reference
    ///     build).
    /// </summary>
    public const int AdvancedElixirSort = 4;

    public const int PurpleJadeItemId = 1024;
    public const int RedJadeItemId = 1025;

    public const int AdvancedElixirRequiredQuantity = 10;

    /// <summary>
    ///     Verified REAL rate (report 04 flags this exact family as "20% (commented 5%)" -- the comment lies, the code
    ///     rolls 20).
    /// </summary>
    public const int AdvancedElixirSuccessRatePercent = 20;

    /// <summary>Result is uniform over item ids 801..806 inclusive (<c>801 + rand_mir()%6</c>).</summary>
    public const int AdvancedElixirResultBaseItemId = 801;

    public const int AdvancedElixirResultRange = 6;

    /// <summary>The 6 base elixirs MK_ELIXIR_NEW accepts (S04_MyWork02.cpp:4408-4413).</summary>
    public static readonly IReadOnlySet<int> AdvancedElixirBaseItemIds =
        new HashSet<int> { 506, 507, 508, 578, 579, 509 };
}

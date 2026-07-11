using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Npcs;

/// <summary>
///     The single global War-Point shop price table (<c>USE_WAR_POINT_SYSTEM</c>) loaded once at boot and read
///     lock-free thereafter -- Fenrir's analogue of legacy's <c>LoadWPItem</c>/<c>mWarPointItem</c> boot-load
///     (<c>Server/ts25zone/S04_MyWork05.cpp:6-37</c>, <c>Server/Header/WarPointSystem.h:74-228</c>). Only the
///     four War-Point NPCs (<see cref="WarPointNpcIds" />) sell out of this table; every other NPC is an
///     ordinary shop driven by <c>world.Npcs</c> (see <see cref="NpcShopPolicy" />).
/// </summary>
/// <remarks>
///     CATALOGUE NOW POPULATED (wave-11 C13-warpoint-prices contract). <see cref="BuildProductionEntries" />
///     reproduces the full, verbatim 48-row <c>WAR_POINT_ITEM_INFO[3][28]</c> table
///     (<c>WarPointSystem.h:74-140</c>; 84 total slots, 36 left at their zero-initialized default and therefore
///     not represented as rows here) plus its per-NPC display gating: catalogue page 0 (10 rows) and page 2 (14
///     rows) each display at all four War-Point NPCs; catalogue page 1 (24 rows, three 8-item tribe-exclusive
///     "Purple" equipment sets) displays each 8-item group at exactly one of <see cref="NobleDragonNpcId" />/
///     <see cref="RoyalSerpentNpcId" />/<see cref="GrandTigerNpcId" /> and never at <see cref="NangimNpcId" />
///     (<c>WarPointSystem.h:39-125</c>) -- confirmed by the contract's own summary: Nangin displays page 0 + page
///     2 (24 distinct items), each tribe NPC displays page 0 + its own 8 page-1 items + page 2 (32 distinct
///     items). Every one of the 48 live rows sets its Contribution-Point price to 0 -- the dual-currency
///     mechanism (<see cref="WarPointShopPolicy" />) is fully implemented and exercised by a synthetic
///     Contribution-Point-priced catalogue in tests, but no shipped row actually charges Contribution Points; the
///     only dual-priced rows in the legacy source are commented-out/dead code
///     (<c>WarPointSystem.h:143-156</c>), never compiled into any build.
///     <para>
///         TWO OPEN GAPS remain, both explicitly flagged as unrecoverable in the contract rather than guessed at:
///         (1) item display <b>names</b> for page 0 (8101/8102/8106/2397/1103/8408/8407/8406/1126/1243) and page 2
///         (86700-86723) are not present in any cited legacy file (page 0's own inline comments are all the
///         identical copy-pasted placeholder "page1 row1 col1", not real names) -- recoverable only from the
///         item-data table, not opened for this contract; (2) whether items 86700-86723 actually reach the
///         non-stackable purchase branch (and therefore receive the fixed <c>Improve=40, Combine=12</c> item-state
///         stamp legacy applies there) depends on their Sort value in the item-data table, also not opened for this
///         contract -- see <see cref="WarPointShopPolicy.IsFixedStateStampItem" />, which flags the id band without
///         inventing the stamp.
///     </para>
/// </remarks>
public sealed class WarPointShopCatalog
{
    /// <summary>NG / Nangin (<c>WarPointSystem.h:18-29,45-48</c>) -- page-2 tribe items never display here.</summary>
    public const int NangimNpcId = 52;

    /// <summary>ND / Noble Dragon -- owns one page-2 "Purple" tribe set.</summary>
    public const int NobleDragonNpcId = 102;

    /// <summary>RS / Royal Serpent -- owns one page-2 "Purple" tribe set.</summary>
    public const int RoyalSerpentNpcId = 202;

    /// <summary>GT / Grand Tiger -- owns one page-2 "Purple" tribe set.</summary>
    public const int GrandTigerNpcId = 302;

    /// <summary><c>MAX_NPC_SELL_PAGE_NUM</c> (<c>WarPointSystem.h:8</c>) -- shop-display page count.</summary>
    public const int MaxPageCount = 3;

    /// <summary><c>MAX_NPC_SELL_SLOT_NUM</c> (<c>WarPointSystem.h:9</c>) -- slots per shop-display page.</summary>
    public const int MaxSlotPerPage = 28;

    /// <summary>The four War-Point NPC indices (<c>WarPoint_NPC</c>, <c>WarPointSystem.h:18-29</c>).</summary>
    public static readonly FrozenSet<int> WarPointNpcIds =
        new[] { NangimNpcId, NobleDragonNpcId, RoyalSerpentNpcId, GrandTigerNpcId }.ToFrozenSet();

    /// <summary>All four War-Point NPCs -- catalogue page 0 and page 2 rows display here.</summary>
    private static readonly ImmutableArray<int> AllFourNpcs =
        [NangimNpcId, NobleDragonNpcId, RoyalSerpentNpcId, GrandTigerNpcId];

    /// <summary>Catalogue page-1 "ND Purple" tribe set -- displays only at <see cref="NobleDragonNpcId" />.</summary>
    private static readonly ImmutableArray<int> NobleDragonOnly = [NobleDragonNpcId];

    /// <summary>Catalogue page-1 "RS Purple" tribe set -- displays only at <see cref="RoyalSerpentNpcId" />.</summary>
    private static readonly ImmutableArray<int> RoyalSerpentOnly = [RoyalSerpentNpcId];

    /// <summary>Catalogue page-1 "GT Purple" tribe set -- displays only at <see cref="GrandTigerNpcId" />.</summary>
    private static readonly ImmutableArray<int> GrandTigerOnly = [GrandTigerNpcId];

    private readonly FrozenDictionary<int, WarPointPriceEntry> _pricesByItemId;

    public WarPointShopCatalog(IEnumerable<WarPointPriceEntry> entries)
    {
        _pricesByItemId = entries.ToFrozenDictionary(entry => entry.ItemId);
    }

    /// <summary>
    ///     The production catalogue, built once at boot from the verbatim 48-row legacy table (see class
    ///     remarks). Registered as a DI singleton (see this workstream's wiring manifest) and consumed by
    ///     <c>WarPointShopService</c>.
    /// </summary>
    /// <remarks>
    ///     Declared AFTER the <c>AllFourNpcs</c>/<c>NobleDragonOnly</c>/<c>RoyalSerpentOnly</c>/
    ///     <c>GrandTigerOnly</c> static fields above on purpose -- C# initializes static members in textual
    ///     declaration order, and <see cref="BuildProductionEntries" /> reads all four, so declaring this
    ///     property first would build every row against an uninitialized (default/empty) display-set field.
    /// </remarks>
    public static WarPointShopCatalog Production { get; } = new(BuildProductionEntries());

    /// <summary>Number of populated price rows -- 48 in the shipped build (10 + 24 + 14).</summary>
    public int Count => _pricesByItemId.Count;

    /// <summary>
    ///     War-Point pricing applies only when the transacting NPC is one of the four War-Point NPCs
    ///     (<c>WarPointSystem.h:18-29</c>). Any other NPC is an ordinary shop.
    /// </summary>
    public static bool IsWarPointNpc(int npcId)
    {
        return WarPointNpcIds.Contains(npcId);
    }

    /// <summary>
    ///     Affordability/price is looked up from this single global table keyed by item index, independent of
    ///     which NPC is transacting (<c>WarPointSystem.h:163-228</c>). An item absent from the table is not a
    ///     War-Point item and is handled by the ordinary purchase path instead.
    /// </summary>
    public bool TryGetPrice(int itemId, out WarPointPriceEntry entry)
    {
        return _pricesByItemId.TryGetValue(itemId, out entry);
    }

    /// <summary>
    ///     The verbatim <c>WAR_POINT_ITEM_INFO[3][28]</c> live catalogue (<c>WarPointSystem.h:79-140</c>), 48
    ///     populated rows out of 84 total slots. Every row's Contribution-Point price is 0 (see class remarks).
    /// </summary>
    private static IReadOnlyList<WarPointPriceEntry> BuildProductionEntries()
    {
        return
        [
            // Catalogue page 0 (10 rows, WarPointSystem.h:79-88) -- universal, displays at all four NPCs. Item
            // names are not recoverable (source comments are the identical copy-pasted placeholder "page1 row1
            // col1", not real names -- see class remarks' open gap #1).
            new WarPointPriceEntry(8101, 200, 0, AllFourNpcs),
            new WarPointPriceEntry(8102, 200, 0, AllFourNpcs),
            new WarPointPriceEntry(8106, 200, 0, AllFourNpcs),
            new WarPointPriceEntry(2397, 50, 0, AllFourNpcs),
            new WarPointPriceEntry(1103, 250, 0, AllFourNpcs),
            new WarPointPriceEntry(8408, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(8407, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(8406, 50, 0, AllFourNpcs),
            new WarPointPriceEntry(1126, 200, 0, AllFourNpcs),
            new WarPointPriceEntry(1243, 150, 0, AllFourNpcs),

            // Catalogue page 1 (24 rows, WarPointSystem.h:90-125) -- three tribe-exclusive 8-item "Purple"
            // equipment sets, all priced 11,570 WP / 0 CP. Never displayed at NangimNpcId (WarPointSystem.h:39-72).
            new WarPointPriceEntry(87077, 11570, 0, NobleDragonOnly), // ND Purple Sword
            new WarPointPriceEntry(87078, 11570, 0, NobleDragonOnly), // ND Purple Blade
            new WarPointPriceEntry(87079, 11570, 0, NobleDragonOnly), // ND Purple Marble
            new WarPointPriceEntry(87080, 11570, 0, NobleDragonOnly), // ND Purple Armor
            new WarPointPriceEntry(87081, 11570, 0, NobleDragonOnly), // ND Purple Gloves
            new WarPointPriceEntry(87082, 11570, 0, NobleDragonOnly), // ND Purple Boots
            new WarPointPriceEntry(87083, 11570, 0, NobleDragonOnly), // ND Purple Ring
            new WarPointPriceEntry(87084, 11570, 0, NobleDragonOnly), // ND Purple Amulet
            new WarPointPriceEntry(87099, 11570, 0, RoyalSerpentOnly), // RS Purple Sword
            new WarPointPriceEntry(87100, 11570, 0, RoyalSerpentOnly), // RS Purple Blade
            new WarPointPriceEntry(87101, 11570, 0, RoyalSerpentOnly), // RS Purple Marble
            new WarPointPriceEntry(87102, 11570, 0, RoyalSerpentOnly), // RS Purple Armor
            new WarPointPriceEntry(87103, 11570, 0, RoyalSerpentOnly), // RS Purple Gloves
            new WarPointPriceEntry(87104, 11570, 0, RoyalSerpentOnly), // RS Purple Boots
            new WarPointPriceEntry(87105, 11570, 0, RoyalSerpentOnly), // RS Purple Ring
            new WarPointPriceEntry(87106, 11570, 0, RoyalSerpentOnly), // RS Purple Amulet
            new WarPointPriceEntry(87121, 11570, 0, GrandTigerOnly), // GT Purple Sword
            new WarPointPriceEntry(87122, 11570, 0, GrandTigerOnly), // GT Purple Blade
            new WarPointPriceEntry(87123, 11570, 0, GrandTigerOnly), // GT Purple Marble
            new WarPointPriceEntry(87124, 11570, 0, GrandTigerOnly), // GT Purple Armor
            new WarPointPriceEntry(87125, 11570, 0, GrandTigerOnly), // GT Purple Gloves
            new WarPointPriceEntry(87126, 11570, 0, GrandTigerOnly), // GT Purple Boots
            new WarPointPriceEntry(87127, 11570, 0, GrandTigerOnly), // GT Purple Ring
            new WarPointPriceEntry(87128, 11570, 0, GrandTigerOnly), // GT Purple Amulet

            // Catalogue page 2 (14 rows, WarPointSystem.h:127-140) -- universal, displays at all four NPCs, all
            // priced 0 WP / 0 CP. A 15th row (86725, 20 WP) and a larger 90786-90794 block are commented-out/dead
            // code (WarPointSystem.h:141,143-156) -- never compiled, deliberately NOT included here.
            new WarPointPriceEntry(86700, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86703, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86704, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86705, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86707, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86709, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86712, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86713, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86714, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86716, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86718, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86721, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86722, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86723, 0, 0, AllFourNpcs)
        ];
    }
}

/// <summary>
///     One War-Point price row: the item, its War-Point cost, its (currently always 0) Contribution-Point cost,
///     and which War-Point NPCs display it (the page-gating that decides whether a request at a given NPC clears
///     the "item must be in this NPC's display grid" gate -- <c>WarPointSystem.h:39-124</c>).
/// </summary>
public readonly record struct WarPointPriceEntry(
    int ItemId,
    int WarPointPrice,
    int ContributionPointPrice,
    ImmutableArray<int> DisplayNpcIds)
{
    /// <summary>
    ///     True when this entry is displayed at <paramref name="npcId" /> -- the per-NPC grid gate. A page-2
    ///     "Purple" tribe entry returns false at any NPC other than its single owning tribe NPC (and always at
    ///     <see cref="WarPointShopCatalog.NangimNpcId" />), reproducing the wrong-NPC disconnect.
    /// </summary>
    public bool DisplaysAtNpc(int npcId)
    {
        return !DisplayNpcIds.IsDefaultOrEmpty && DisplayNpcIds.Contains(npcId);
    }
}

using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Reward-table DATA for item 8113 (Over-Enchant Box) -- workstream C10-remaining-boxes, a sibling data
///     file extending <see cref="LootBoxCatalog" /> per that catalog's own "contract-first, never-invent"
///     posture (its own class remarks). The open MECHANISM (single-open roll, placement, one-box consumption,
///     double GL_606 audit, zone mirror) is entirely pre-built and untouched by this file:
///     <see cref="Inventory.UseItems.Boxes.LootBoxOpenResolver" /> /
///     <see cref="Inventory.UseItems.Boxes.LootBoxUseItemHandler" />,
///     via <see cref="LootBoxRewardResolver.RollUniform" /> -- the same primitive box 635/76542 already use. This
///     is the one item of this workstream's four that maps onto an EXISTING <see cref="BoxRewardKind" /> with no
///     mechanism change at all: a flat, tribe-independent, level-independent uniform pick over a fixed 9-id pool.
///     See this workstream's wiring manifest for the one-line splice into <see cref="LootBoxCatalog" />'s specs
///     list -- deliberately NOT made here (concurrent sibling waves touch that same file; the splice is a single
///     serial-integration-pass step).
/// </summary>
/// <remarks>
///     Réf. C++ (C10-remaining-boxes contract) : Server/ts25zone/S04_MyWork03.cpp:1697-1711 (bulk-path
///     <c>OpenSingleBoxNoStellar</c>, item 8113's 9-entry pool) and :7823-7861 (single-path production case
///     block) -- both confirmed byte-for-byte identical by the contract's own direct comparison. Item 8113 is
///     already present in <see cref="LootBoxCatalog.BulkOpenWhitelist" /> (Server/ts25zone/S04_MyWork03.cpp:1291-1316,
///     <c>IsBulkBoxNoStellar</c>, id at :1299), so no whitelist change is needed either -- only this data file
///     plus the specs-list splice.
///     <para>
///         No tribe or level gate applies to this box (contract's own Preconditions section: "No precondition
///         beyond the shared ones above... gates item 8113 specifically -- its pool carries no tribe or level
///         requirement of its own"), and every one of its 9 pool entries always resolves to a found catalog item
///         (contract's own Edge cases section), so no rejection path specific to this item id exists beyond the
///         mechanism's own shared gates (tick throttle, slot-quantity check, catalog-lookup safety net).
///     </para>
///     <para>
///         Grant shape per the contract's own Side effects/Outputs sections: quantity 0, "value" 0, no sockets, no
///         expiry (<see cref="BoxRewardSpec.RentalDays" /> left at its default of 0 -- none of these 9 ids fall in
///         the recognized rentable-id range), and a serial generated from the resolved reward's own real catalog
///         item-type (every one of the 9 outcomes qualifies for a genuine non-zero serial per the contract). Serial
///         generation itself is NOT modeled anywhere in the current box-open mechanism --
///         <c>BoxRewardPlacementResolver.BuildStack</c>
///         hardcodes Serial to 0 for every box today, a pre-existing gap shared by every already-registered box
///         (601/7105/76542), not something newly introduced by this item -- flagged in this workstream's
///         openQuestions rather than silently worked around here.
///     </para>
/// </remarks>
public static class OverEnchantBox8113RewardTable
{
    /// <summary>world.Items id for the Over-Enchant Box itself.</summary>
    public const int BoxItemId = 8113;

    /// <summary>
    ///     The 9-entry uniform pool (Server/ts25zone/S04_MyWork03.cpp:1703-1711 / :7831-7842): every draw is
    ///     exactly 1-in-9, no rarity skew, no tribe or level dependence.
    /// </summary>
    public static readonly ImmutableArray<int> RewardItemIds =
        [8101, 8102, 8106, 1103, 1126, 1166, 1222, 1237, 828];

    /// <summary>
    ///     The ready-to-register <see cref="BoxRewardSpec" /> for box 8113. Add this to
    ///     <see cref="LootBoxCatalog" />'s <c>specs</c> list (see this workstream's wiring manifest) to complete
    ///     registration -- no other file needs a change for this item specifically, since
    ///     <c>RegisteredBoxIds</c>, <c>LootBoxUseItemHandler.HandledItemIds</c>, and
    ///     <c>UseItemHandlerRegistry</c>'s constructor loop all derive from that list automatically, and 8113 is
    ///     already present in <see cref="LootBoxCatalog.BulkOpenWhitelist" />.
    /// </summary>
    public static readonly BoxRewardSpec Spec = BoxRewardSpec.Uniform(BoxItemId, RewardItemIds);
}

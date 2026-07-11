using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Reward-table DATA for item 1240 ("Pill Lucky Bag") -- workstream C10-remaining-box-pools, a sibling data
///     file extending <see cref="LootBoxCatalog" /> per that catalog's own "contract-first, never-invent"
///     posture (its own class remarks). The open MECHANISM (single-open roll, bulk-open, placement, one-box
///     consumption, double GL_606 audit, zone mirror) is entirely pre-built and untouched by this file:
///     <see cref="Inventory.UseItems.Boxes.LootBoxOpenResolver" /> /
///     <see cref="Inventory.UseItems.Boxes.LootBoxUseItemHandler" />, via
///     <see cref="Consumables.LootBoxRewardResolver.RollUniform" /> -- the same primitive box 635/76542/8113
///     already use. This is the simplest box of this workstream's eight: no tribe dependence, no pity, and both
///     legacy copies are byte-identical, no divergence to reason about at all.
/// </summary>
/// <remarks>
///     Réf. C++ (C10-remaining-box-pools contract) : Server/ts25zone/S04_MyWork03.cpp:1498-1502 (bulk-path copy)
///     and :6379-6384 (single-path copy) -- both confirmed byte-identical single statements by the contract's
///     own direct re-read: the reward is always <c>GetRandomElixirNoMP()</c>, a flat uniform draw over
///     506/508/509/578/579 (20% each). Item 507 is deliberately excluded from this pool -- it belongs only to
///     the separate, unrelated <c>GetRandomElixir()</c> 506-507-508-509-578-579 pool used elsewhere in the same
///     source file (Server/ts25zone/S04_MyWork03.cpp:7308,7311-7313), a different helper with a different (six-
///     item) pool that this box's own reward routine never calls. Item 1240 is already present in
///     <see cref="LootBoxCatalog.BulkOpenWhitelist" /> (Server/ts25zone/S04_MyWork03.cpp:1291-1316,
///     <c>IsBulkBoxNoStellar</c>), so no whitelist change is needed either -- only this data file plus the
///     specs-list splice.
///     <para>
///         Grant shape: no rental/expiry stamp recorded by the contract for this box's reward (unlike e.g. box
///         76542's 3-day rental) -- <see cref="BoxRewardSpec.RentalDays" /> is left at its default of 0. No pity
///         counter, no faction/tribe dependence -- confirmed by the contract's own Edge cases section ("No
///         pity counter, no divergence between copies").
///     </para>
/// </remarks>
public static class PillLuckyBag1240RewardTable
{
    /// <summary>world.Items id for the Pill Lucky Bag itself.</summary>
    public const int BoxId = 1240;

    /// <summary>
    ///     <c>GetRandomElixirNoMP()</c>'s 5-entry uniform pool (Server/ts25zone/S04_MyWork03.cpp:7315-7317, cited
    ///     by the C10-remaining-box-pools contract): every draw is exactly 1-in-5 (20%), no rarity skew. Item 507
    ///     is deliberately excluded -- see this type's own remarks.
    /// </summary>
    public static readonly ImmutableArray<int> RewardItemIds = [506, 508, 509, 578, 579];

    /// <summary>
    ///     The ready-to-register <see cref="BoxRewardSpec" /> for box 1240. Add this to
    ///     <see cref="LootBoxCatalog" />'s <c>specs</c> list to complete registration -- no other file needs a
    ///     change for this item specifically, since <c>RegisteredBoxIds</c>,
    ///     <c>LootBoxUseItemHandler.HandledItemIds</c>, and <c>UseItemHandlerRegistry</c>'s constructor loop all
    ///     derive from that list automatically, and 1240 is already present in
    ///     <see cref="LootBoxCatalog.BulkOpenWhitelist" />.
    /// </summary>
    public static readonly BoxRewardSpec Spec = BoxRewardSpec.Uniform(BoxId, RewardItemIds);
}

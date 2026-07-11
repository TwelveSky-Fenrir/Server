using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     The one shared "place a just-rolled loot-box reward into inventory" routine every box family in this
///     document funnels through, regardless of which weighted table produced the winning item id. No I/O, no
///     Zone dependency -- callers resolve the reward item id/stackability/rent-expiry first (catalog lookup),
///     then hand the already-resolved shape here.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:5271-5430 (<c>MyWork__SetBoxChangeInventoryItem99</c> --
///     reward lookup, stack-merge-vs-fresh-slot decision, box consumption) ; Server/ts25zone/S04_MyWork05.cpp:
///     5199-5269 (<c>BoxReward_IsStackable99</c>, <c>BoxReward_FindStackSlot99</c> -- stack-merge eligibility
///     and the box's-own-slot exclusion) ; Server/Header/Protocol/DEFINE.h:611 (999-per-stack ceiling).
/// </remarks>
public static class BoxRewardPlacementResolver
{
    public enum Outcome
    {
        /// <summary>Merged into an existing same-id stack elsewhere in inventory.</summary>
        Merged,

        /// <summary>Written fresh into a previously-empty slot.</summary>
        PlacedInEmptySlot,

        /// <summary>No existing mergeable stack and no empty slot anywhere -- box not consumed, no reward granted.</summary>
        InventoryFull
    }

    /// <summary><c>MAX_ITEM_DUPLICATION_NUM</c> (DEFINE.h:611).</summary>
    public const int MaxStackQuantity = 999;

    /// <summary>
    ///     world.Items.Sort for a pet-classified item (STRUCT.h:1696) -- the same constant
    ///     <c>GmPetExperienceGrantService.PetItemCatalogSort</c> reads. A pet reward's slot quantity is the
    ///     legacy "activity" value, clamped to <see cref="MaxPetActivity" />.
    /// </summary>
    public const byte PetSort = 22;

    /// <summary>The pet-activity ceiling <c>SetItemQuantity</c> clamps a pet reward's quantity to (0..100).</summary>
    public const int MaxPetActivity = 100;

    /// <summary>
    ///     <c>SetItemQuantity</c> (Server/ts25zone/S04_MyWork05.cpp:5150-5197): the reward's slot quantity is
    ///     decided purely by its <paramref name="rewardSort" />, independent of how many the roll nominally
    ///     produced. A stackable reward (<c>ContainerMatrix.IsStackableSort</c> -- sort 2, or sort 99 with
    ///     materials-stacking, which this codebase always treats as stackable) is clamped into
    ///     1..<see cref="MaxStackQuantity" />; a pet (<see cref="PetSort" />) into 0..<see cref="MaxPetActivity" />;
    ///     every other sort (equipment, cosmetics, single-use items -- whose slot quantity carries no meaning) is
    ///     zeroed. <paramref name="rolledQuantity" /> is the pre-clamp count (1 for an ordinary single-box open);
    ///     the returned <see cref="QuantityResult.IsStackable" /> is what a caller then threads into
    ///     <see cref="ResolvedReward.IsStackable" />.
    /// </summary>
    public static QuantityResult ResolveQuantity(byte rewardSort, int rolledQuantity)
    {
        if (ContainerMatrix.IsStackableSort(rewardSort))
            return new QuantityResult(Math.Clamp(rolledQuantity, 1, MaxStackQuantity), true);

        if (rewardSort == PetSort)
            return new QuantityResult(Math.Clamp(rolledQuantity, 0, MaxPetActivity), false);

        return new QuantityResult(0, false);
    }

    /// <summary>
    ///     Searches both inventory pages for a mergeable same-id stack first (excluding the box's own slot, per
    ///     <c>BoxReward_FindStackSlot99</c>); falling back to the first empty slot found, page0 before page1,
    ///     scanning slot 0 upward within each page.
    /// </summary>
    /// <param name="secondPageAccessible">
    ///     C1-vault-expiry-enforcement: whether the dated-vault last inventory page (page1) currently passes
    ///     its expiry gate (<c>state.InventoryDate &gt;= GameDate.Today()</c>) -- mirrors the greater-than-or-
    ///     equal comparator <c>FindEmptyInvenForItem</c> uses (Server/ts25zone/S07_MyGame03.cpp:4557-4563) and
    ///     the page-skip <c>BoxReward_FindStackSlot99</c> applies to the same expired page
    ///     (Server/ts25zone/S04_MyWork05.cpp:5218-5243): when <see langword="false" />, page1 is excluded from
    ///     BOTH the merge scan and the empty-slot scan, exactly as if it did not exist -- never touched,
    ///     never migrated, simply unreachable through this placement search while expired. Defaults to
    ///     <see langword="true" /> (page1 always searched) so every pre-existing caller/test that has no
    ///     concept of a per-character expiry date keeps its prior behavior unchanged.
    /// </param>
    public static Result Resolve(ResolvedReward reward, byte boxContainer, byte boxSlot,
        ImmutableDictionary<byte, ItemStack> page0, ImmutableDictionary<byte, ItemStack> page1,
        bool secondPageAccessible = true)
    {
        if (reward.IsStackable &&
            TryFindMergeSlot(reward, boxContainer, boxSlot, ContainerMatrix.InventoryPage0, page0, out var merge0))
            return merge0;

        if (secondPageAccessible && reward.IsStackable &&
            TryFindMergeSlot(reward, boxContainer, boxSlot, ContainerMatrix.InventoryPage1, page1, out var merge1))
            return merge1;

        if (TryFindEmptySlot(ContainerMatrix.InventoryPage0, page0, out var empty0Slot))
            return new Result(Outcome.PlacedInEmptySlot, ContainerMatrix.InventoryPage0, empty0Slot,
                BuildStack(reward));

        if (secondPageAccessible && TryFindEmptySlot(ContainerMatrix.InventoryPage1, page1, out var empty1Slot))
            return new Result(Outcome.PlacedInEmptySlot, ContainerMatrix.InventoryPage1, empty1Slot,
                BuildStack(reward));

        return new Result(Outcome.InventoryFull, 0, 0, null);
    }

    private static bool TryFindMergeSlot(ResolvedReward reward, byte boxContainer, byte boxSlot,
        byte searchContainer, ImmutableDictionary<byte, ItemStack> container, out Result result)
    {
        foreach (var (slot, stack) in container)
        {
            // BoxReward_FindStackSlot99: a stack sitting in the box's own slot is deliberately excluded, to
            // avoid merging into a slot about to be zeroed by the box's own removal.
            if (searchContainer == boxContainer && slot == boxSlot)
                continue;

            if (stack.ItemId != reward.ItemId)
                continue;

            var merged = stack.Quantity + reward.Quantity;
            if (merged > MaxStackQuantity)
                continue;

            result = new Result(Outcome.Merged, searchContainer, slot, stack with { Quantity = merged });
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryFindEmptySlot(byte container, ImmutableDictionary<byte, ItemStack> current,
        out byte emptySlot)
    {
        ContainerMatrix.TryGetMaxSlot(container, out var maxSlotInclusive);
        for (var slot = 0; slot <= maxSlotInclusive; slot++)
        {
            if (current.ContainsKey((byte)slot))
                continue;

            emptySlot = (byte)slot;
            return true;
        }

        emptySlot = 0;
        return false;
    }

    private static ItemStack BuildStack(ResolvedReward reward)
    {
        // A stackable reward is always stripped of any caller-supplied serial (S04_MyWork05.cpp:5271-5321's
        // own stackable-safety step -- the same step that strips value/socket/expiry) -- only a non-stackable
        // reward can carry one through, e.g. the Lucky Ticket family's fixed per-ticket constant tag
        // (Server/ts25zone/S04_MyWork03.cpp:3478). Every pre-C9-tickets caller passes Serial 0 either way, so
        // this changes nothing for them.
        var serial = reward.IsStackable ? 0 : reward.Serial;
        return new ItemStack(reward.ItemId, reward.Quantity, reward.Enchant, reward.Combine, reward.Refine,
            reward.Socket, 0, 0, 0, reward.ExpireDate, serial);
    }

    /// <summary>Output of <see cref="ResolveQuantity" />: the clamped slot quantity and whether the reward stacks.</summary>
    public readonly record struct QuantityResult(int Quantity, bool IsStackable);

    /// <summary>
    ///     Already-resolved shape of the reward the caller rolled -- see this type's own remarks.
    ///     <paramref name="Serial" /> defaults to 0 (every pre-C9-tickets caller's prior behavior); a
    ///     non-default value is only ever honored for a non-stackable reward -- see <see cref="BuildStack" />.
    /// </summary>
    public readonly record struct ResolvedReward(
        int ItemId,
        int Quantity,
        bool IsStackable,
        byte Enchant,
        byte Combine,
        byte Refine,
        byte Socket,
        int ExpireDate,
        int Serial = 0);

    /// <summary>NewStack is null only for <see cref="Outcome.InventoryFull" />.</summary>
    public readonly record struct Result(Outcome Outcome, byte Container, byte Slot, ItemStack? NewStack)
    {
        public bool Succeeded => Outcome is Outcome.Merged or Outcome.PlacedInEmptySlot;
    }
}

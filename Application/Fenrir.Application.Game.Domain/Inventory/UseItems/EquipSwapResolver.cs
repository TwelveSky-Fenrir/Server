using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     Pure decision for the op23 double-click-to-equip family (the equipment categories of the legacy
///     sort-based switch): confirm the idle action state and equip eligibility, derive the target equip slot
///     from the item's own equip-part tag, and describe the four-field + three-socket-gem + expiry swap
///     between the addressed inventory slot and that equip slot. No I/O — the handler applies the swap and
///     recomputes stats.
///     <para>
///         Unlike the drag-to-equip path (tSort 210, <c>GenericActionService.MoveContainerAsync</c>), which
///         rejects an occupied destination outright, this IS a swap: the previously-equipped piece lands back
///         in the addressed inventory slot. Because <see cref="ItemStack" /> already carries the two auxiliary
///         fields, the quantity, the value/serial, the three socket gems, and the expiry date, swapping whole
///         <see cref="ItemStack" /> values is exactly the legacy's four-field + three-gem + expiry swap.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2356-2441 (the equip-type action switch: idle gate,
///     CheckPossibleEquipItem eligibility with tEquipIndex == -1, the four-field + three-gem + expiry swap,
///     stat/HP-MP recompute, and the accessory-slot effect clear) ; Server/ts25zone/S07_MyGame03.cpp:9-40
///     (<c>MyUtil::Init</c> populating <c>mEquipPart[0..12]</c>, the slot-to-tag table inverted here) ;
///     Server/ts25zone/S04_MyWork03.cpp:2374-2385 (equip categories 28-33, gated on the <c>MG5ORIGIN</c> build
///     macro — build-variant status unverified in the originating contract, but harmlessly covered here since
///     they fall inside <see cref="EquipCategoryHigh" />=33). Two sub-effects of the cited swap are NOT modeled
///     and are flagged for a follow-up finding: (a) "set the action type from the weapon class" needs a
///     weapon-class-to-action-type table the contract does not supply; (b) clearing the derived accessory
///     effect value when the swapped slot is the accessory-effect slot (equip index 7) needs a
///     <see cref="PlayerRuntimeState" /> effect field the contract does not name.
/// </remarks>
public static class EquipSwapResolver
{
    public enum Outcome
    {
        Success,

        /// <summary>The avatar was not in the idle/ready action pose (legacy aAction.aSort != 1).</summary>
        NotIdle,

        /// <summary>The item failed the tribe/level/rebirth/final-category equip gate.</summary>
        NotEquippable,

        /// <summary>The item's equip-part tag maps to no real double-click-equip slot.</summary>
        InvalidTargetSlot
    }

    /// <summary>mEquipPart[0..12] idle/ready action pose sentinel (aAction.aSort == 1).</summary>
    private const int IdleActionSort = 1;

    /// <summary>
    ///     ReturnItemSort(...) placeholder — the same documented 0 <c>GenericActionService</c> passes: its own
    ///     derivation is outside the equip gate's citation range, so passing 0 only skips the rebirth-12
    ///     classification sub-check (every other equip check still runs for real).
    /// </summary>
    private const int ItemSortClassificationPlaceholder = 0;

    /// <summary>iSort final-category whitelist bounds — mirror of <c>EquipItemValidationGate</c>'s own private pair.</summary>
    private const int EquipCategoryLow = 6;

    private const int EquipCategoryHigh = 33;

    /// <summary>
    ///     mEquipPart[0..12] (S07_MyGame03.cpp:9-40) — a private mirror of the identically-sourced table inside
    ///     <c>EquipItemValidationGate</c>; the slot index is the inverse lookup of this table by the item's
    ///     EquipInfo2 tag.
    /// </summary>
    private static readonly ImmutableArray<byte> EquipPartTagBySlot = [2, 3, 4, 5, 6, 7, 0, 9, 10, 11, 12, 13, 14];

    /// <summary>Does this item resolve to a double-click-equip candidate? Used by the registry to route it here.</summary>
    public static bool ClaimsItem(ItemRowDto item)
    {
        return item.Sort is >= EquipCategoryLow and <= EquipCategoryHigh &&
               TryDeriveEquipSlot(item.EquipInfo2, out _);
    }

    /// <summary>
    ///     Maps an item's equip-part tag (EquipInfo2) onto its 0-12 equip-slot index. Tag 0 is deliberately not
    ///     a target: it is both the pet slot's sentinel (mEquipPart[6] == 0) and the default EquipInfo2 of every
    ///     non-equippable item, so treating it as "no slot" stops the dispatcher stealing every unclaimed
    ///     EquipInfo2==0 item into the equip path.
    /// </summary>
    public static bool TryDeriveEquipSlot(byte equipPartTag, out byte slot)
    {
        if (equipPartTag != 0)
            for (byte i = 0; i < EquipPartTagBySlot.Length; i++)
                if (EquipPartTagBySlot[i] == equipPartTag)
                {
                    slot = i;
                    return true;
                }

        slot = 0;
        return false;
    }

    public static Result Resolve(
        ItemStack inventoryItem,
        EquipItemValidationGate.EquipCandidate? candidate,
        ImmutableDictionary<byte, ItemStack> equipmentContainer,
        int actionSort,
        byte characterTribe,
        int combinedLevel,
        int rebirthCount)
    {
        if (actionSort != IdleActionSort)
            return new Result(Outcome.NotIdle, 0, default, null);

        var gate = EquipItemValidationGate.Evaluate(candidate, ItemSortClassificationPlaceholder, characterTribe,
            EquipItemValidationGate.SkipSlotCheck, combinedLevel, rebirthCount);
        if (gate != EquipItemValidationGate.Outcome.Success)
            return new Result(Outcome.NotEquippable, 0, default, null);

        if (candidate is not { } resolved || !TryDeriveEquipSlot((byte)resolved.EquipPartTag, out var targetSlot))
            return new Result(Outcome.InvalidTargetSlot, 0, default, null);

        var previouslyEquipped = equipmentContainer.TryGetValue(targetSlot, out var equipped)
            ? equipped
            : (ItemStack?)null;

        return new Result(Outcome.Success, targetSlot, inventoryItem, previouslyEquipped);
    }

    /// <param name="TargetEquipSlot">The 0-12 equip-slot index the used item moves into.</param>
    /// <param name="NewEquipStack">The stack to write into <see cref="TargetEquipSlot" /> (the used item).</param>
    /// <param name="NewInventoryStack">
    ///     The stack to write back into the addressed inventory slot (the previously-equipped piece), or null
    ///     to empty that slot (the equip slot was vacant).
    /// </param>
    public readonly record struct Result(
        Outcome Outcome,
        byte TargetEquipSlot,
        ItemStack NewEquipStack,
        ItemStack? NewInventoryStack)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}

using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Pure equip-time validation gate -- <c>MyUtil::CheckPossibleEquipItem</c>. Both legacy call sites feed
///     this the same five checks (tribe, slot tag, level, rebirth, final category) before an item may move
///     into an equip slot; no I/O, no session/Zone dependency.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:47-143 (<c>CheckPossibleEquipItem</c>, full body) ;
///     Server/ts25zone/S07_MyGame03.cpp:9-40 (<c>MyUtil::Init</c>, populates <c>mEquipPart[0..12]</c>) ;
///     Server/ts25zone/S04_MyWork05.cpp:1234-1306 (<c>ProcessForInventoryToEquip</c>, the explicit-slot call
///     site -- <c>tEquipIndex</c> a real 0-12 index, failure disconnects via <c>Quit()</c>) ;
///     Server/ts25zone/S04_MyWork03.cpp:2356-2429 (the "use item" equip-type action switch --
///     <c>tEquipIndex</c> always -1, failure disconnects via <c>Quit()</c>) ;
///     ServerDocs/12_ts25zone/11_MyGame03_PartieA.md §5.1 (independent prior summary, cross-checked against a
///     direct read; source of the "res_sort 1/2/4/8/29" rebirth-gated classification codes used below).
///     <para>
///     Two things this gate deliberately does NOT model, both left to the caller: (1) the avatar's
///     "action-sort ready" precondition -- a distinct, softer no-op outcome (no disconnect) checked by both
///     call sites before this gate even runs, whose exact numeric "ready" sentinel was not part of the
///     originating contract; and (2) the source/destination bounds checks that precede this gate entirely.
///     </para>
///     <para>
///     <paramref name="itemSortClassification"/> (see <see cref="Evaluate"/>) stands in for the legacy
///     <c>ReturnItemSort(...)</c> helper -- that helper's own derivation logic (mapping a raw item onto one
///     of its classification codes) is a separate function outside this contract's citation range, so it is
///     accepted here as a caller-supplied, already-computed value rather than re-derived.
///     </para>
/// </remarks>
public static class EquipItemValidationGate
{
    public enum Outcome
    {
        ItemNotFound,
        WrongTribe,
        WrongSlotTag,
        LevelTooLow,
        RebirthTooLow,
        CategoryNotEquippable,
        Success
    }

    /// <summary>iEquipInfo[0] sentinel meaning "usable by any tribe" -- skips the tribe-match comparison entirely.</summary>
    public const int AnyTribeSentinel = 1;

    /// <summary>Offset subtracted from iEquipInfo[0] before comparing against the character's own tribe.</summary>
    public const int TribeRestrictionOffset = 2;

    /// <summary>tEquipIndex sentinel meaning "skip the slot-tag check" -- the "use item" call site's own value.</summary>
    public const int SkipSlotCheck = -1;

    private const int MinSlotIndex = 0;
    private const int MaxSlotIndex = 12;

    private const int FinalCategoryLow = 6;
    private const int FinalCategoryHigh = 33;

    /// <summary>iCheckSetItem value that independently gates at rebirth&gt;=12.</summary>
    private const int SetItemRebirthGateValue = 3;

    /// <summary>mEquipPart[0..12], MyUtil::Init (S07_MyGame03.cpp:9-40).</summary>
    private static readonly ImmutableArray<byte> EquipPartTagBySlot =
        [2, 3, 4, 5, 6, 7, 0, 9, 10, 11, 12, 13, 14];

    /// <summary>
    ///     ReturnItemSort(...) classification codes that each independently gate at rebirth&gt;=12
    ///     (ServerDocs/12_ts25zone/11_MyGame03_PartieA.md §5.1: "res_sort 1/2/4/8/29"). A commented-out,
    ///     never-compiled rule for three further codes is intentionally not reproduced here.
    /// </summary>
    private static readonly ImmutableHashSet<int> Rebirth12ClassificationCodes = [1, 2, 4, 8, 29];

    /// <summary>
    ///     One item's equip-relevant reference-table fields -- the subset of <c>ITEM_INFO</c> this gate reads.
    ///     A <see langword="null"/> value stands for <c>mITEM.Search(iIndex)</c> returning nothing.
    /// </summary>
    /// <param name="ItemId">iIndex -- also the key for the hardcoded per-id rebirth gates.</param>
    /// <param name="TribeRestriction">iEquipInfo[0].</param>
    /// <param name="EquipPartTag">iEquipInfo[1].</param>
    /// <param name="LevelLimit">iLevelLimit.</param>
    /// <param name="MartialLevelLimit">iMartialLevelLimit.</param>
    /// <param name="CheckSetItem">iCheckSetItem.</param>
    /// <param name="Sort">iSort -- both the final-category whitelist input and (indirectly) ReturnItemSort's own input.</param>
    public readonly record struct EquipCandidate(
        int ItemId,
        int TribeRestriction,
        int EquipPartTag,
        int LevelLimit,
        int MartialLevelLimit,
        int CheckSetItem,
        int Sort);

    /// <param name="item">The resolved item reference-table row, or null if the id did not resolve.</param>
    /// <param name="itemSortClassification">Pre-computed ReturnItemSort(...) result -- see remarks.</param>
    /// <param name="characterTribe">The equipping character's tribe.</param>
    /// <param name="targetEquipSlotIndex">
    ///     A real 0-12 equip-slot index, or <see cref="SkipSlotCheck"/> to skip the slot-tag check entirely
    ///     (the "use item" call site's own convention -- the true slot is derived by the caller instead).
    /// </param>
    /// <param name="characterCombinedLevel">Sum of the character's two level-tracking fields.</param>
    /// <param name="characterRebirthCount">The character's rebirth count.</param>
    public static Outcome Evaluate(
        EquipCandidate? item,
        int itemSortClassification,
        byte characterTribe,
        int targetEquipSlotIndex,
        int characterCombinedLevel,
        int characterRebirthCount)
    {
        if (item is not { } resolved)
            return Outcome.ItemNotFound;

        if (resolved.TribeRestriction != AnyTribeSentinel &&
            resolved.TribeRestriction - TribeRestrictionOffset != characterTribe)
            return Outcome.WrongTribe;

        if (targetEquipSlotIndex != SkipSlotCheck)
        {
            if (targetEquipSlotIndex is < MinSlotIndex or > MaxSlotIndex)
                return Outcome.WrongSlotTag; // caller should already have bounds-checked; defensive fallback only
            if (resolved.EquipPartTag != EquipPartTagBySlot[targetEquipSlotIndex])
                return Outcome.WrongSlotTag;
        }

        if (resolved.LevelLimit + resolved.MartialLevelLimit > characterCombinedLevel)
            return Outcome.LevelTooLow;

        if (!PassesRebirthGate(resolved, itemSortClassification, characterRebirthCount))
            return Outcome.RebirthTooLow;

        if (resolved.Sort < FinalCategoryLow || resolved.Sort > FinalCategoryHigh)
            return Outcome.CategoryNotEquippable;

        return Outcome.Success;
    }

    private static bool PassesRebirthGate(EquipCandidate item, int itemSortClassification, int rebirth)
    {
        switch (item.ItemId)
        {
            case 13553 or 33553 or 53553: return rebirth >= 6;
            case 13554 or 33554 or 53554: return rebirth >= 12;
            case >= 87206 and <= 87213 or >= 87228 and <= 87235 or >= 87250 and <= 87257: return rebirth >= 12;
            case 86754 or 86756 or 86758: return rebirth >= 6;
            case 86755 or 86757 or 86759: return rebirth >= 12;
            case 2303 or 2304 or 2305: return rebirth >= 7;
        }

        if (item.CheckSetItem == SetItemRebirthGateValue && rebirth < 12)
            return false;

        if (Rebirth12ClassificationCodes.Contains(itemSortClassification) && rebirth < 12)
            return false;

        // Dead in every compiled build variant, intentionally not reproduced:
        //  - a commented-out id range 216-218 that would have required rebirth >= 7;
        //  - a commented-out three-classification-code rule that would have required rebirth >= 12;
        //  - item id 99200's reject, gated behind a macro never defined anywhere in this repository.
        return true;
    }
}

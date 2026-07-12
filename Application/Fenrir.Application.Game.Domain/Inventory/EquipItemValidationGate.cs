using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory;

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

    public const int AnyTribeSentinel = 1;

    public const int TribeRestrictionOffset = 2;

    public const int SkipSlotCheck = -1;

    public const int ItemSortClassificationNotComputed = 0;

    private const int MinSlotIndex = 0;
    private const int MaxSlotIndex = 12;

    private const int FinalCategoryLow = 6;
    private const int FinalCategoryHigh = 33;

    private const int SetItemRebirthGateValue = 3;

    private static readonly ImmutableArray<byte> EquipPartTagBySlot =
        [2, 3, 4, 5, 6, 7, 0, 9, 10, 11, 12, 13, 14];

    private static readonly ImmutableHashSet<int> Rebirth12ClassificationCodes = [1, 2, 4, 8, 29];

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
                return Outcome.WrongSlotTag;
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

        return true;
    }

    public readonly record struct EquipCandidate(
        int ItemId,
        int TribeRestriction,
        int EquipPartTag,
        int LevelLimit,
        int MartialLevelLimit,
        int CheckSetItem,
        int Sort);
}

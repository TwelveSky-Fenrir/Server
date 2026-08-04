using System.Collections.Immutable;
using Fenrir.Domain.Game.Items;

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

    public const int ItemSortClassificationNotComputed = 0;

    private const int FinalCategoryLow = 6;
    private const int FinalCategoryHigh = 33;

    private const int SetItemRebirthGateValue = 3;

    private static readonly ImmutableHashSet<int> Rebirth12ClassificationCodes = [1, 2, 4, 8];

    public static Outcome Evaluate(
        EquipCandidate? item,
        byte characterPreviousTribe,
        int targetEquipSlotIndex,
        int characterCombinedLevel,
        int characterRebirthCount)
    {
        return Evaluate(item, ItemSortClassificationNotComputed, characterPreviousTribe, targetEquipSlotIndex,
            characterCombinedLevel, characterRebirthCount);
    }

    public static Outcome Evaluate(
        EquipCandidate? item,
        int itemSortClassificationFallback,
        byte characterPreviousTribe,
        int targetEquipSlotIndex,
        int characterCombinedLevel,
        int characterRebirthCount)
    {
        return Evaluate(item, itemSortClassificationFallback, characterPreviousTribe, true, targetEquipSlotIndex,
            characterCombinedLevel, characterRebirthCount);
    }

    public static Outcome EvaluateWithoutSlotCheck(
        EquipCandidate? item,
        byte characterPreviousTribe,
        int characterCombinedLevel,
        int characterRebirthCount)
    {
        return Evaluate(item, ItemSortClassificationNotComputed, characterPreviousTribe, false, 0,
            characterCombinedLevel, characterRebirthCount);
    }

    private static Outcome Evaluate(
        EquipCandidate? item,
        int itemSortClassificationFallback,
        byte characterPreviousTribe,
        bool checkSlotTag,
        int targetEquipSlotIndex,
        int characterCombinedLevel,
        int characterRebirthCount)
    {
        if (item is not { } resolved)
            return Outcome.ItemNotFound;

        if (resolved.TribeRestriction != AnyTribeSentinel &&
            resolved.TribeRestriction - TribeRestrictionOffset != characterPreviousTribe)
            return Outcome.WrongTribe;

        if (checkSlotTag && !EquipmentSlots.MatchesSlotTag(resolved.EquipPartTag, targetEquipSlotIndex))
            return Outcome.WrongSlotTag;

        if (resolved.LevelLimit + resolved.MartialLevelLimit > characterCombinedLevel)
            return Outcome.LevelTooLow;

        var classification = resolved.Type is { } itemType
            ? ItemSortClassifier.Classify(itemType, resolved.EquipPartTag, resolved.ItemId, resolved.Sort)
            : itemSortClassificationFallback;

        if (!PassesRebirthGate(resolved, classification, characterRebirthCount))
            return Outcome.RebirthTooLow;

        if (resolved.Sort < FinalCategoryLow || resolved.Sort > FinalCategoryHigh)
            return Outcome.CategoryNotEquippable;

        return Outcome.Success;
    }

    private static bool PassesRebirthGate(EquipCandidate item, int itemSortClassification, int rebirth)
    {
        var minimumRebirthForItemId = item.ItemId switch
        {
            13553 or 33553 or 53553 => 6,
            13554 or 33554 or 53554 => 12,
            >= 87206 and <= 87213 or >= 87228 and <= 87235 or >= 87250 and <= 87257 => 12,
            86754 or 86756 or 86758 => 6,
            86755 or 86757 or 86759 => 12,
            2303 or 2304 or 2305 => 7,
            _ => 0
        };

        if (rebirth < minimumRebirthForItemId)
            return false;

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
        int Sort,
        int? Type = null)
    {
        public static EquipCandidate FromRow(ItemRowDto item)
        {
            return new EquipCandidate(item.ItemId, item.EquipInfo1, item.EquipInfo2, item.LevelLimit,
                item.MartialLevelLimit, item.CheckSetItem, item.Sort, item.Type);
        }
    }
}

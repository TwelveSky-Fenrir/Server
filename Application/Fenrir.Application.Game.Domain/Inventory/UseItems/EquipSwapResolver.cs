using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public static class EquipSwapResolver
{
    public enum Outcome
    {
        Success,

        NotIdle,

        NotEquippable,

        InvalidTargetSlot
    }

    private const int IdleActionSort = 1;

    private const int ItemSortClassificationPlaceholder = 0;

    private const int EquipCategoryLow = 6;

    private const int EquipCategoryHigh = 33;

    private static readonly ImmutableArray<byte> EquipPartTagBySlot = [2, 3, 4, 5, 6, 7, 0, 9, 10, 11, 12, 13, 14];

    public static bool ClaimsItem(ItemRowDto item)
    {
        return item.Sort is >= EquipCategoryLow and <= EquipCategoryHigh &&
               TryDeriveEquipSlot(item.EquipInfo2, out _);
    }

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

    public readonly record struct Result(
        Outcome Outcome,
        byte TargetEquipSlot,
        ItemStack NewEquipStack,
        ItemStack? NewInventoryStack)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}

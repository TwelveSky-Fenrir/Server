using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Inventory;

/// <summary>Builds ObjectForAvatar.EquipForView (int[13][2] of ItemId/Enchant pairs) from the Equipment container.</summary>
public static class EquipmentViewCodec
{
    private const int EquipmentSlotCount = 13;

    /// <summary>From freshly-loaded world-entry SQL rows (Equipment container only).</summary>
    public static int[] BuildEquipForView(IReadOnlyList<CharacterItemSlotDto> items)
    {
        var view = new int[EquipmentSlotCount * 2];

        foreach (var item in items)
        {
            if (item.Container != ContainerMatrix.Equipment || item.Slot >= EquipmentSlotCount)
                continue;

            view[item.Slot * 2] = item.ItemId;
            view[item.Slot * 2 + 1] = item.Enchant;
        }

        return view;
    }

    /// <summary>From the live in-memory Equipment container.</summary>
    public static int[] BuildEquipForView(IReadOnlyDictionary<byte, ItemStack> equipmentContainer)
    {
        var view = new int[EquipmentSlotCount * 2];

        foreach (var (slot, stack) in equipmentContainer)
        {
            if (slot >= EquipmentSlotCount)
                continue;

            view[slot * 2] = stack.ItemId;
            view[slot * 2 + 1] = stack.Enchant;
        }

        return view;
    }
}

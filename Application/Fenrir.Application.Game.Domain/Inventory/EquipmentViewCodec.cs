using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class EquipmentViewCodec
{
    private const int EquipmentSlotCount = 13;

        public static int[] BuildEquipForView(IReadOnlyList<CharacterItemSlotDto> items)
    {
        var view = new int[EquipmentSlotCount * 2];

        foreach (var item in items)
        {
            if (item.Container != ContainerMatrix.Equipment || item.Slot >= EquipmentSlotCount)
                continue;

            view[item.Slot * 2] = item.ItemId;
            view[item.Slot * 2 + 1] = item.Slot == PetSlots.EquipmentSlot
                ? ItemValueCodec.Encode(item.Enchant, item.Combine, item.Refine, item.Socket)
                : item.Enchant;
        }

        return view;
    }

        public static int[] BuildEquipForView(IReadOnlyDictionary<byte, ItemStack> equipmentContainer)
    {
        var view = new int[EquipmentSlotCount * 2];

        foreach (var (slot, stack) in equipmentContainer)
        {
            if (slot >= EquipmentSlotCount)
                continue;

            view[slot * 2] = stack.ItemId;
            view[slot * 2 + 1] = slot == PetSlots.EquipmentSlot
                ? ItemValueCodec.Encode(stack.Enchant, stack.Combine, stack.Refine, stack.Socket)
                : stack.Enchant;
        }

        return view;
    }
}

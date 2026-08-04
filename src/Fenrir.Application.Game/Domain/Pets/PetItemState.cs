using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Domain.Pets;

public static class PetItemState
{
    public static int Growth(ItemStack stack)
    {
        return Growth(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
    }

    public static int Growth(byte enchant, byte combine, byte refine, byte socket)
    {
        return ItemValueCodec.Encode(enchant, combine, refine, socket);
    }

    public static byte Activity(ItemStack stack)
    {
        return (byte)Math.Clamp(stack.Quantity, 0, ItemQuantityPolicy.MaxPetActivity);
    }

    public static ItemStack WithState(ItemStack stack, int growth, byte activity)
    {
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(growth);
        return stack with
        {
            Quantity = activity,
            Enchant = enchant,
            Combine = combine,
            Refine = refine,
            Socket = socket
        };
    }

    public static void SynchronizeEquippedState(InventoryState inventory, int growth, byte activity)
    {
        var equipment = inventory.GetContainer(ContainerMatrix.Equipment);
        if (!equipment.TryGetValue(PetSlots.EquipmentSlot, out var pet))
            return;

        inventory.ReplaceContainer(ContainerMatrix.Equipment,
            equipment.SetItem(PetSlots.EquipmentSlot, WithState(pet, growth, activity)));
    }
}

using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Pets;

public static class PetSlots
{
    public const byte EquipmentSlot = 8;

    public static int ResolveEquippedPetItemId(IReadOnlyList<CharacterItemSlotDto> items)
    {
        foreach (var item in items)
            if (item.Container == ContainerMatrix.Equipment && item.Slot == EquipmentSlot)
                return item.ItemId;

        return 0;
    }
}

using Fenrir.Application.Game.Inventory;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Pets;

/// <summary>
///     The pet/Phoenix-amulet equipment slot (FEQUIP_TYPE::EPET). Same slot StatCalculator's Phoenix-amulet
///     contribution reads, so a growable pet and a Phoenix amulet are mutually exclusive, never double-counted.
/// </summary>
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

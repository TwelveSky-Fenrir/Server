using Fenrir.Application.Game.Inventory;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Pets;

/// <summary>
///     The pet/Phoenix-amulet equipment slot -- <c>FEQUIP_TYPE::EPET</c> (STRUCT.h:1662-1676: EAMULET=0,
///     ECAPE=1, EARMOR=2, EGLOVES=3, ERING=4, EBOOTS=5, ENULL=6, EWEAPON=7, EPET=8, EDECO1-4=9-12) --
///     verified to be the SAME slot index <see cref="Stats.StatCalculator" />'s own <c>bySlot[8]</c>
///     "petAmulet"/Phoenix contribution already reads (items 76005-76007, iSort 28): a player can only ever
///     have ONE item equipped here, so a growable pet (iSort 22, this module) and a Phoenix amulet are
///     mutually exclusive occupants of the exact same slot, never double-counted.
/// </summary>
public static class PetSlots
{
    public const byte EquipmentSlot = 8;

    /// <summary>Item id currently equipped in the pet slot (0 = none) -- scans a flat item-row list (world-entry bundle shape), not a live container.</summary>
    public static int ResolveEquippedPetItemId(IReadOnlyList<CharacterItemSlotDto> items)
    {
        foreach (var item in items)
            if (item.Container == ContainerMatrix.Equipment && item.Slot == EquipmentSlot)
                return item.ItemId;

        return 0;
    }
}

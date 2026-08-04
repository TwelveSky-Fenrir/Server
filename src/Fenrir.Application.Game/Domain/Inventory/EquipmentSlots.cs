using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class EquipmentSlots
{
    public const byte AmuletSlot = 0;
    public const byte CapeSlot = 1;
    public const byte ArmorSlot = 2;
    public const byte GlovesSlot = 3;
    public const byte RingSlot = 4;
    public const byte BootsSlot = 5;

    public const byte UnusedSlot = 6;
    public const byte WeaponSlot = 7;
    public const byte PetSlot = 8;
    public const byte Deco1Slot = 9;
    public const byte Deco2Slot = 10;
    public const byte Deco3Slot = 11;
    public const byte Deco4Slot = 12;

    public const int Count = 13;

    public const byte NoTag = 0;

    public static readonly ImmutableArray<byte> EquipPartTagBySlot =
    [
        2, 3, 4, 5, 6, 7, NoTag, 9, 10, 11, 12, 13, 14
    ];

    public static bool IsValidSlot(int slot)
    {
        return slot is >= 0 and < Count;
    }

    public static bool MatchesSlotTag(int equipPartTag, int slot)
    {
        return IsValidSlot(slot) && equipPartTag == EquipPartTagBySlot[slot];
    }

    public static bool TryGetSlotForTag(int equipPartTag, out byte slot)
    {
        if (equipPartTag != NoTag)
            for (byte i = 0; i < Count; i++)
                if (EquipPartTagBySlot[i] == equipPartTag)
                {
                    slot = i;
                    return true;
                }

        slot = 0;
        return false;
    }
}

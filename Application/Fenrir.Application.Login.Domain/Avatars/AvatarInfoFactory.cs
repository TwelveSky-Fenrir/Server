using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Login.Domain.Avatars;

public static class AvatarInfoFactory
{
    private const int EquipSlotCount = 13;
    private const int EquipWireIntsPerSlot = 4;

    private const int PetEquipSlot = 8;

    private const int InventorySlotsPerPage = 64;
    private const int InventoryWireIntsPerSlot = 6;

    private const int InventoryPageCount = 2;

    private const int StorePageCount = 2;
    private const int StoreSlotsPerPage = 28;
    private const int StoreWireIntsPerSlot = 4;

    private const byte ContainerInventoryPage0 = 0;
    private const byte ContainerInventoryPage1 = 1;
    private const byte ContainerEquipment = 2;
    private const byte ContainerStorePage0 = 3;
    private const byte ContainerStorePage1 = 4;

    private const int SkillSlotCount = 40;
    private const int SkillWireIntsPerSlot = 2;

    private const int HotkeyPageCount = 3;
    private const int HotkeyKeysPerPage = 14;
    private const int HotkeyWireIntsPerSlot = 3;

    private static bool IsLoginRosterBlacklistedItemId(int itemId) => itemId is 1451 or 2268;

    public static AvatarInfo Zeroed => AvatarInfoTemplates.Zeroed;

    public static AvatarInfo CreateForCharacter(CharacterWorldEntryDto character)
    {
        return Zeroed with
        {
            Name = character.Name,
            Tribe = character.Tribe,
            Gender = character.Gender,
            HeadType = character.HeadType,
            FaceType = character.FaceType,
            Level1 = character.Level,
            LogoutInfo =
            [
                character.MapId,
                (int)character.PosX,
                (int)character.PosY,
                (int)character.PosZ,
                character.Life,
                character.Mana
            ]
        };
    }

    public static int[] BuildEquipArray(IReadOnlyList<CharacterItemSlotTvp> equipment, int petGrowth = 0,
        byte petActivity = 0)
    {
        var equip = new int[EquipSlotCount * EquipWireIntsPerSlot];

        foreach (var item in equipment)
        {
            if (item.Slot >= EquipSlotCount)
                continue;

            var baseIndex = item.Slot * EquipWireIntsPerSlot;
            equip[baseIndex] = item.ItemId;

            if (item.Slot == PetEquipSlot)
            {
                equip[baseIndex + 1] = petActivity;
                equip[baseIndex + 2] = petGrowth;
            }
            else
            {
                equip[baseIndex + 1] = item.ExpireDate;
                equip[baseIndex + 2] = item.Enchant | (item.Combine << 8) | (item.Refine << 16) | (item.Socket << 24);
            }
        }

        return equip;
    }

    public static int[] BuildInventoryArray(IReadOnlyList<CharacterItemSlotTvp> page0Items)
    {
        var inventory = new int[2 * InventorySlotsPerPage * InventoryWireIntsPerSlot];

        foreach (var item in page0Items)
        {
            if (item.Slot >= InventorySlotsPerPage)
                continue;

            var baseIndex = item.Slot * InventoryWireIntsPerSlot;
            inventory[baseIndex] = item.ItemId;
            inventory[baseIndex + 3] = item.Quantity;
            inventory[baseIndex + 4] = item.Enchant | (item.Combine << 8) | (item.Refine << 16) | (item.Socket << 24);
        }

        return inventory;
    }

    public static int[] BuildSkillArray(IReadOnlyList<CharacterSkillSlotTvp> skills)
    {
        var skill = new int[SkillSlotCount * SkillWireIntsPerSlot];

        foreach (var row in skills)
        {
            if (row.SlotIndex >= SkillSlotCount)
                continue;

            var baseIndex = row.SlotIndex * SkillWireIntsPerSlot;
            skill[baseIndex] = row.SkillId;
            skill[baseIndex + 1] = row.Grade;
        }

        return skill;
    }

    public static int[] BuildEquipArrayFromRosterItems(IReadOnlyList<CharacterRosterItemDto> items,
        int petGrowth = 0, byte petActivity = 0)
    {
        var equip = new int[EquipSlotCount * EquipWireIntsPerSlot];

        foreach (var item in items)
        {
            if (item.Container != ContainerEquipment || item.Slot >= EquipSlotCount)
                continue;

            if (IsLoginRosterBlacklistedItemId(item.ItemId))
                continue;

            var baseIndex = item.Slot * EquipWireIntsPerSlot;
            equip[baseIndex] = item.ItemId;

            if (item.Slot == PetEquipSlot)
            {
                equip[baseIndex + 1] = petActivity;
                equip[baseIndex + 2] = petGrowth;
            }
            else
            {
                equip[baseIndex + 1] = item.ExpireDate;
                equip[baseIndex + 2] = item.Enchant | (item.Combine << 8) | (item.Refine << 16) | (item.Socket << 24);
            }
        }

        return equip;
    }

    public static int[] BuildInventoryArrayFromRosterItems(IReadOnlyList<CharacterRosterItemDto> items)
    {
        var inventory = new int[InventoryPageCount * InventorySlotsPerPage * InventoryWireIntsPerSlot];

        foreach (var item in items)
        {
            var page = item.Container switch
            {
                ContainerInventoryPage0 => 0,
                ContainerInventoryPage1 => 1,
                _ => -1
            };

            if (page < 0 || item.Slot >= InventorySlotsPerPage)
                continue;

            if (IsLoginRosterBlacklistedItemId(item.ItemId))
                continue;

            var baseIndex = (page * InventorySlotsPerPage + item.Slot) * InventoryWireIntsPerSlot;
            inventory[baseIndex] = item.ItemId;
            inventory[baseIndex + 3] = item.Quantity;
            inventory[baseIndex + 4] = item.Enchant | (item.Combine << 8) | (item.Refine << 16) | (item.Socket << 24);
        }

        return inventory;
    }

    public static int[] BuildStoreItemArrayFromRosterItems(IReadOnlyList<CharacterRosterItemDto> items)
    {
        var store = new int[StorePageCount * StoreSlotsPerPage * StoreWireIntsPerSlot];

        foreach (var item in items)
        {
            var page = item.Container switch
            {
                ContainerStorePage0 => 0,
                ContainerStorePage1 => 1,
                _ => -1
            };

            if (page < 0 || item.Slot >= StoreSlotsPerPage)
                continue;

            if (IsLoginRosterBlacklistedItemId(item.ItemId))
                continue;

            var baseIndex = (page * StoreSlotsPerPage + item.Slot) * StoreWireIntsPerSlot;
            store[baseIndex] = item.ItemId;
            store[baseIndex + 1] = item.ExpireDate;
            store[baseIndex + 2] = item.Enchant | (item.Combine << 8) | (item.Refine << 16) | (item.Socket << 24);
        }

        return store;
    }

    public static int[] BuildHotKeyArray(IReadOnlyList<CharacterHotkeySlotTvp> hotkeys)
    {
        var hotkey = new int[HotkeyPageCount * HotkeyKeysPerPage * HotkeyWireIntsPerSlot];

        foreach (var row in hotkeys)
        {
            if (row.Page >= HotkeyPageCount || row.KeyIndex >= HotkeyKeysPerPage)
                continue;

            var baseIndex = (row.Page * HotkeyKeysPerPage + row.KeyIndex) * HotkeyWireIntsPerSlot;
            hotkey[baseIndex] = row.Sort;
            hotkey[baseIndex + 1] = row.Value1;
            hotkey[baseIndex + 2] = row.Value2;
        }

        return hotkey;
    }
}

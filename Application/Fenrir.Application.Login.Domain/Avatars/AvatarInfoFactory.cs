using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     Builds the AVATAR_INFO wire payload from a persisted character; zero-template shared with GameServer via
///     <see cref="AvatarInfoTemplates" />.
/// </summary>
public static class AvatarInfoFactory
{
    // FEQUIP_TYPE (Server/Header/Protocol/STRUCT.h:1662-1676): amulet/cape/armor/gloves/ring/boots/an unused
    // slot/weapon/pet/4 decoration slots -- 13 slots total, each packed as 4 wire ints. Universal wire-shape
    // constants, not per-race data (bridge-stats-equipment contract, gap-table row 7); this is an independent
    // duplication of the same constants on GameServer's own AvatarInfoFactory, not a shared one, since Login
    // has no reference to the Game project.
    private const int EquipSlotCount = 13;
    private const int EquipWireIntsPerSlot = 4;

    // FEQUIP_TYPE::EPET, index 8 -- same independent-duplication posture as EquipSlotCount/EquipWireIntsPerSlot
    // above (Game's own copy lives at Fenrir.Application.Game.Domain.Pets.PetSlots.EquipmentSlot).
    private const int PetEquipSlot = 8;

    // wAvatar.aInventory[2][64][6] -- ServerDocs/12_ts25zone/06_MyWork03_Items_Equipement_Craft_Quetes.md:
    // 115-117 ("Format d'inventaire, 6 entiers par slot... [0] ID objet, [3] quantité/durabilité, [4] valeur
    // d'amélioration/option, [5] réservé"); total wire size cross-checked against ServerDocs/19_Header_Lib/
    // 06_FunctionH_Utilitaires_XOR.md:276's MAX_INVENTORY_PAGE_NUM * MAX_INVENTORY_SLOT_NUM *
    // MAX_INVENTORY_VALUE_NUM = 2*64*6 = 768, matching AvatarInfo.Inventory's own FixedArray(768). Same
    // "[1]/[2] left at wire-zero, not established by any available citation" open question as GameServer's
    // own AvatarInfoFactory.BuildInventoryArrayFromRows -- see that method's remarks for the full citation.
    // Only page 0 is modeled: usp_Character_CreateWithStarterKit always inserts starter-kit inventory rows at
    // Container=0 (Database/StoredProcedures/game/usp_Character_CreateWithStarterKit.sql), so no starter kit
    // ever populates page 1 -- there is nothing to project there at creation time, not a gap.
    private const int InventorySlotsPerPage = 64;
    private const int InventoryWireIntsPerSlot = 6;

    // Both inventory pages (unlike the creation-time-only, page-0-only BuildInventoryArray above -- a returning
    // character's account-login roster read can carry occupied rows on page 1 too, see
    // BuildInventoryArrayFromRosterItems' own remarks).
    private const int InventoryPageCount = 2;

    // wAvatar.aStoreItem[2][28][4] -- same ItemId/ExpireDate/packed-upgrade-byte/unused convention as
    // aEquip[13][4] above (ServerDocs/19_Header_Lib/06_FunctionH_Utilitaires_XOR.md:277's
    // MAX_STORE_ITEM_PAGE_NUM * MAX_STORE_ITEM_SLOT_NUM * MAX_STORE_ITEM_VALUE_NUM = 2*28*4 = 224, matching
    // AvatarRosterResponse.StoreItem's own FixedArray(224)) -- the same citation GameServer's own
    // AvatarInfoFactory.StorePageCount/StoreSlotsPerPage/StoreWireIntsPerSlot already established, duplicated
    // here (not shared) since Login has no reference to the Game project.
    private const int StorePageCount = 2;
    private const int StoreSlotsPerPage = 28;
    private const int StoreWireIntsPerSlot = 4;

    // game.CharacterItems.Container convention (Application.Game.Domain.Inventory.ContainerMatrix's own
    // header comment, duplicated here for the same "no cross-project reference" reason as above): 0/1 the two
    // Inventory pages, 2 Equipment, 3/4 the two Store pages.
    private const byte ContainerInventoryPage0 = 0;
    private const byte ContainerInventoryPage1 = 1;
    private const byte ContainerEquipment = 2;
    private const byte ContainerStorePage0 = 3;
    private const byte ContainerStorePage1 = 4;

    // wAvatar.aSkill[40][2] -- Database/Tables/game/CharacterSkills.sql:1's own citation
    // ("[slot][0]=SkillId, [slot][1]=Grade"); 40*2 = 80 matches AvatarInfo.Skill's own FixedArray(80).
    private const int SkillSlotCount = 40;
    private const int SkillWireIntsPerSlot = 2;

    // wAvatar.aHotKey[3][14][3] -- Database/Tables/game/CharacterHotkeys.sql:1's own citation
    // ("3 pages x 14 keys x 3 ints... Sort/Value1/Value2 stored verbatim as the legacy triple");
    // 3*14*3 = 126 matches AvatarInfo.HotKey's own FixedArray(126).
    private const int HotkeyPageCount = 3;
    private const int HotkeyKeysPerPage = 14;
    private const int HotkeyWireIntsPerSlot = 3;
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

    /// <summary>
    ///     Projects the equipment rows CreateAvatarHandler is about to persist onto AVATAR_INFO's aEquip[13][4]
    ///     wire array -- independent re-implementation of GameServer's own
    ///     AvatarInfoFactory.BuildEquipArrayFromRows/PackUpgradeBytes (that one reads back CharacterItemSlotDto
    ///     rows from the DB; this one reads the CharacterItemSlotTvp rows the handler already holds in memory, one
    ///     request earlier in the same flow). The 4th int per slot is unmapped in both, left at wire-zero.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1131-1135 -- the pet slot's 2nd/3rd wire ints are the
    ///     pet's activity/growth as plain values (NOT the expiration-date/packed-Enchant-Combine-Refine-Socket
    ///     pair every other slot carries there), since those two values live on the character record itself
    ///     rather than on the pet's own CharacterItems row (<paramref name="petGrowth" />/<paramref name="petActivity" />
    ///     come from the caller's already-known creation-time literals, not from <paramref name="equipment" />'s
    ///     own zeroed Enchant/Combine/Refine/Socket/ExpireDate fields for that row). Defaults to 0/0 so callers
    ///     with no pet in scope (e.g. existing unit tests) are unaffected.
    /// </remarks>
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

    /// <summary>
    ///     Projects the starter-kit inventory rows CreateAvatarService is about to persist (always Container=0,
    ///     see <see cref="InventorySlotsPerPage" />'s own remarks) onto AVATAR_INFO's aInventory[2][64][6] wire
    ///     array -- independent re-implementation of GameServer's own
    ///     AvatarInfoFactory.BuildInventoryArrayFromRows, the same "reads the in-memory TVP rows one request
    ///     earlier in the same flow" relationship <see cref="BuildEquipArray" /> already has to
    ///     BuildEquipArrayFromRows.
    /// </summary>
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

    /// <summary>See <see cref="SkillSlotCount" />'s own remarks for the citation.</summary>
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

    /// <summary>
    ///     Projects a returning character's persisted equipment rows (usp_Character_GetAccountRoster's RS1,
    ///     pre-filtered by the caller to one character's rows only) onto the character-select roster's
    ///     aEquip[13][4] wire array -- same per-slot mapping as <see cref="BuildEquipArray" /> above, sourced
    ///     from freshly-read <see cref="CharacterRosterItemDto" /> rows instead of the in-memory
    ///     <see cref="CharacterItemSlotTvp" /> rows a creation response already holds.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1131-1135 -- the pet slot's 2nd/3rd wire ints are the
    ///     pet's activity/growth as plain values (NOT the expiration-date/packed-Enchant-Combine-Refine-Socket
    ///     pair every other slot carries there), since those two values live on the character record itself
    ///     rather than on the pet's own CharacterItems row. Independent re-implementation of GameServer's own
    ///     AvatarInfoFactory.BuildEquipArrayFromRows (same citation) -- Login has no project reference to Game.
    /// </remarks>
    public static int[] BuildEquipArrayFromRosterItems(IReadOnlyList<CharacterRosterItemDto> items,
        int petGrowth = 0, byte petActivity = 0)
    {
        var equip = new int[EquipSlotCount * EquipWireIntsPerSlot];

        foreach (var item in items)
        {
            if (item.Container != ContainerEquipment || item.Slot >= EquipSlotCount)
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

    /// <summary>
    ///     Both inventory pages (Container 0/1) from usp_Character_GetAccountRoster's RS1, unlike
    ///     <see cref="BuildInventoryArray" />'s creation-time page-0-only projection -- a returning character
    ///     can genuinely have occupied rows on page 1 (e.g. the bonus-page rental grant), which a fresh
    ///     creation response never needs to represent.
    /// </summary>
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

            var baseIndex = (page * InventorySlotsPerPage + item.Slot) * InventoryWireIntsPerSlot;
            inventory[baseIndex] = item.ItemId;
            inventory[baseIndex + 3] = item.Quantity;
            inventory[baseIndex + 4] = item.Enchant | (item.Combine << 8) | (item.Refine << 16) | (item.Socket << 24);
        }

        return inventory;
    }

    /// <summary>See <see cref="StorePageCount" />'s own remarks for the citation.</summary>
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

            var baseIndex = (page * StoreSlotsPerPage + item.Slot) * StoreWireIntsPerSlot;
            store[baseIndex] = item.ItemId;
            store[baseIndex + 1] = item.ExpireDate;
            store[baseIndex + 2] = item.Enchant | (item.Combine << 8) | (item.Refine << 16) | (item.Socket << 24);
        }

        return store;
    }

    /// <summary>See <see cref="HotkeyPageCount" />'s own remarks for the citation.</summary>
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

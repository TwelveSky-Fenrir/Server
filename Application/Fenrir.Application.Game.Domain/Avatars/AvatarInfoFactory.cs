using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Avatars;

public static class AvatarInfoFactory
{
    private const int MaxGeneralExperience = 2_000_000_000;

    private const int InventoryPageCount = 2;
    private const int InventorySlotsPerPage = 64;
    private const int InventoryWireIntsPerSlot = 6;

    private const int StorePageCount = 2;
    private const int StoreSlotsPerPage = 28;
    private const int StoreWireIntsPerSlot = 4;

    private const int SkillSlotCount = 40;
    private const int SkillWireIntsPerSlot = 2;

    private const int HotkeyPageCount = 3;
    private const int HotkeyKeysPerPage = 14;
    private const int HotkeyWireIntsPerSlot = 3;

    public static AvatarInfo CreateForCharacter(CharacterWorldSnapshotDto character,
        IReadOnlyList<CharacterItemSlotDto> items, AvatarSocialSnapshot? social = null,
        IReadOnlyList<CharacterSkillDto>? skills = null, IReadOnlyList<CharacterHotkeyDto>? hotkeys = null)
    {
        var s = social ?? AvatarSocialSnapshot.Empty;

        return AvatarInfoTemplates.Zeroed with
        {
            Name = character.Name,
            Tribe = character.Tribe,
            PreviousTribe = character.PreviousTribe,
            Gender = character.Gender,
            HeadType = character.HeadType,
            FaceType = character.FaceType,
            Level1 = character.Level,
            Level2 = character.Level2,
            Exp1 = MaxGeneralExperience,
            Exp2 = character.Exp2,
            Vit = character.StatVit,
            Str = character.StatStr,
            Int = character.StatInt,
            Dex = character.StatDex,
            StatPoint = character.StatPoints,
            SkillPoint = character.SkillPoints,
            Money = (int)character.Money,
            InventoryDate = character.InventoryDate,
            StoreDate = character.StoreDate,
            StoreMoney = (int)character.StoreMoney,
            BigMoney = character.BigMoney,
            BigStoreMoney = character.BigStoreMoney,
            Title = character.Title,
            Halo = character.Halo,
            RebirthNum = character.RebirthCount,
            Zone241Time = character.Zone241Time,
            PetBagDate = character.PetBagDate,
            WarPoint = character.WarPoint,
            EatLifePotion = character.EatLifePotion,
            EatManaPotion = character.EatManaPotion,
            EatStrPotion = character.EatStrPotion,
            EatDexPotion = character.EatDexPotion,
            EatElePotion = character.EatElePotion,
            AutoTime2 = character.AutoTime2,
            DoubleExpTime1 = character.DoubleExpTime1,
            DoubleExpTime2 = character.DoubleExpTime2,
            AutoBuffTime = character.AutoBuffTime,
            Premium = character.PremiumExpireUtc,
            Animal = BuildSingleMountSlotArray(character.MountItemId, character.MountSlotIndex),
            AnimalIndex = MountCatalog.ResolveDisplayedSlotMarker(character.MountItemId, character.MountSlotIndex),
            AnimalTime = character.MountTime,
            AnimalPower = BuildSingleMountSlotArray(character.MountPower, character.MountSlotIndex),
            AnimalExpActivity = BuildSingleMountSlotArray(character.MountExpActivity, character.MountSlotIndex),
            Equip = BuildEquipArrayFromRows(items, character.PetGrowth, character.PetActivity),
            Inventory = BuildInventoryArrayFromRows(items),
            StoreItem = BuildStoreItemArrayFromRows(items),
            Skill = BuildSkillArrayFromRows(skills ?? []),
            HotKey = BuildHotKeyArrayFromRows(hotkeys ?? []),
            Friend = s.BuildFriendArray(),
            Teacher = s.Teacher,
            Student = s.Student,
            GuildName = s.GuildName,
            GuildRole = s.GuildRoleWire,
            CallName = s.CallName,
            QuestInfo =
            [
                character.QuestStepPermanent, character.QuestActiveId, character.QuestSort,
                character.QuestTargetPhase, character.QuestKillCounter
            ],
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

    public static AvatarInfo CreateForRuntimeState(PlayerRuntimeState state, short mapId, float posX, float posY,
        float posZ)
    {
        return AvatarInfoTemplates.Zeroed with
        {
            Name = state.Name,
            Tribe = state.Tribe,
            PreviousTribe = state.PreviousTribe,
            Gender = state.Gender,
            HeadType = state.HeadType,
            FaceType = state.FaceType,
            Level1 = state.Level,
            Level2 = state.Level2,
            Exp1 = MaxGeneralExperience,
            Exp2 = state.Exp2,
            Vit = state.StatVit,
            Str = state.StatStr,
            Int = state.StatInt,
            Dex = state.StatDex,
            StatPoint = state.StatPoints,
            SkillPoint = state.SkillPoints,
            Title = state.Title,
            Halo = state.Halo,
            RebirthNum = state.RebirthCount,
            Zone241Time = state.Zone241Time,
            PetBagDate = state.PetBagDate,
            WarPoint = state.WarPoint,
            EatLifePotion = state.EatLifePotion,
            EatManaPotion = state.EatManaPotion,
            EatStrPotion = state.EatStrPotion,
            EatDexPotion = state.EatDexPotion,
            EatElePotion = state.EatElePotion,
            AutoBuffTime = state.AutoBuffTime,
            GuildName = state.GuildName,
            GuildRole = GuildRoleCodec.DbRoleToWire(state.GuildRoleDb),
            CallName = state.GuildCallName,
            Equip = BuildEquipArrayFromContainer(state.Inventory.GetContainer(ContainerMatrix.Equipment),
                state.PetGrowth, state.PetActivity),
            Inventory = BuildInventoryArrayFromContainers(
                state.Inventory.GetContainer(ContainerMatrix.InventoryPage0),
                state.Inventory.GetContainer(ContainerMatrix.InventoryPage1)),
            StoreItem = BuildStoreItemArrayFromContainers(
                state.Inventory.GetContainer(ContainerMatrix.StorePage0),
                state.Inventory.GetContainer(ContainerMatrix.StorePage1)),
            Skill = BuildSkillArrayFromLearnedSkills(state.LearnedSkills),
            QuestInfo =
            [
                state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort, state.QuestTargetPhase,
                state.QuestKillCounter
            ],
            LogoutInfo = [mapId, (int)posX, (int)posY, (int)posZ, state.Life, state.Mana]
        };
    }

    private static int[] BuildSingleMountSlotArray(int value, int slotIndex)
    {
        var slots = new int[10];

        if (slotIndex is >= 0 and < 10)
            slots[slotIndex] = value;

        return slots;
    }

    private static int[] BuildEquipArrayFromRows(IReadOnlyList<CharacterItemSlotDto> items, int petGrowth,
        byte petActivity)
    {
        var equip = new int[52];

        foreach (var item in items)
        {
            if (item.Container != ContainerMatrix.Equipment || item.Slot >= 13)
                continue;

            var baseIndex = item.Slot * 4;
            equip[baseIndex] = item.ItemId;

            if (item.Slot == PetSlots.EquipmentSlot)
            {
                equip[baseIndex + 1] = petActivity;
                equip[baseIndex + 2] = petGrowth;
            }
            else
            {
                equip[baseIndex + 1] = item.ExpireDate;
                equip[baseIndex + 2] = PackUpgradeBytes(item.Enchant, item.Combine, item.Refine, item.Socket);
            }
        }

        return equip;
    }

    private static int[] BuildEquipArrayFromContainer(IReadOnlyDictionary<byte, ItemStack> equipmentContainer,
        int petGrowth, byte petActivity)
    {
        var equip = new int[52];

        foreach (var (slot, stack) in equipmentContainer)
        {
            if (slot >= 13)
                continue;

            var baseIndex = slot * 4;
            equip[baseIndex] = stack.ItemId;

            if (slot == PetSlots.EquipmentSlot)
            {
                equip[baseIndex + 1] = petActivity;
                equip[baseIndex + 2] = petGrowth;
            }
            else
            {
                equip[baseIndex + 1] = stack.ExpireDate;
                equip[baseIndex + 2] = PackUpgradeBytes(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
            }
        }

        return equip;
    }

    private static int PackUpgradeBytes(byte enchant, byte combine, byte refine, byte socket)
    {
        return enchant | (combine << 8) | (refine << 16) | (socket << 24);
    }

    private static int[] BuildInventoryArrayFromRows(IReadOnlyList<CharacterItemSlotDto> items)
    {
        var inventory = new int[InventoryPageCount * InventorySlotsPerPage * InventoryWireIntsPerSlot];

        foreach (var item in items)
        {
            var page = item.Container switch
            {
                ContainerMatrix.InventoryPage0 => 0,
                ContainerMatrix.InventoryPage1 => 1,
                _ => -1
            };

            if (page < 0 || item.Slot >= InventorySlotsPerPage)
                continue;

            var baseIndex = (page * InventorySlotsPerPage + item.Slot) * InventoryWireIntsPerSlot;
            inventory[baseIndex] = item.ItemId;
            inventory[baseIndex + 3] = item.Quantity;
            inventory[baseIndex + 4] = PackUpgradeBytes(item.Enchant, item.Combine, item.Refine, item.Socket);
        }

        return inventory;
    }

    private static int[] BuildInventoryArrayFromContainers(IReadOnlyDictionary<byte, ItemStack> page0,
        IReadOnlyDictionary<byte, ItemStack> page1)
    {
        var inventory = new int[InventoryPageCount * InventorySlotsPerPage * InventoryWireIntsPerSlot];

        FillInventoryPage(inventory, 0, page0);
        FillInventoryPage(inventory, 1, page1);

        return inventory;
    }

    private static void FillInventoryPage(int[] inventory, int page, IReadOnlyDictionary<byte, ItemStack> slots)
    {
        foreach (var (slot, stack) in slots)
        {
            if (slot >= InventorySlotsPerPage)
                continue;

            var baseIndex = (page * InventorySlotsPerPage + slot) * InventoryWireIntsPerSlot;
            inventory[baseIndex] = stack.ItemId;
            inventory[baseIndex + 3] = stack.Quantity;
            inventory[baseIndex + 4] = PackUpgradeBytes(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
        }
    }

    private static int[] BuildStoreItemArrayFromRows(IReadOnlyList<CharacterItemSlotDto> items)
    {
        var store = new int[StorePageCount * StoreSlotsPerPage * StoreWireIntsPerSlot];

        foreach (var item in items)
        {
            var page = item.Container switch
            {
                ContainerMatrix.StorePage0 => 0,
                ContainerMatrix.StorePage1 => 1,
                _ => -1
            };

            if (page < 0 || item.Slot >= StoreSlotsPerPage)
                continue;

            var baseIndex = (page * StoreSlotsPerPage + item.Slot) * StoreWireIntsPerSlot;
            store[baseIndex] = item.ItemId;
            store[baseIndex + 1] = item.ExpireDate;
            store[baseIndex + 2] = PackUpgradeBytes(item.Enchant, item.Combine, item.Refine, item.Socket);
        }

        return store;
    }

    private static int[] BuildStoreItemArrayFromContainers(IReadOnlyDictionary<byte, ItemStack> page0,
        IReadOnlyDictionary<byte, ItemStack> page1)
    {
        var store = new int[StorePageCount * StoreSlotsPerPage * StoreWireIntsPerSlot];

        FillStorePage(store, 0, page0);
        FillStorePage(store, 1, page1);

        return store;
    }

    private static void FillStorePage(int[] store, int page, IReadOnlyDictionary<byte, ItemStack> slots)
    {
        foreach (var (slot, stack) in slots)
        {
            if (slot >= StoreSlotsPerPage)
                continue;

            var baseIndex = (page * StoreSlotsPerPage + slot) * StoreWireIntsPerSlot;
            store[baseIndex] = stack.ItemId;
            store[baseIndex + 1] = stack.ExpireDate;
            store[baseIndex + 2] = PackUpgradeBytes(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
        }
    }

    private static int[] BuildSkillArrayFromRows(IReadOnlyList<CharacterSkillDto> skills)
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

    private static int[] BuildSkillArrayFromLearnedSkills(IReadOnlyDictionary<byte, LearnedSkill> learnedSkills)
    {
        var skill = new int[SkillSlotCount * SkillWireIntsPerSlot];

        foreach (var (slotIndex, learned) in learnedSkills)
        {
            if (slotIndex >= SkillSlotCount)
                continue;

            var baseIndex = slotIndex * SkillWireIntsPerSlot;
            skill[baseIndex] = learned.SkillId;
            skill[baseIndex + 1] = learned.Grade;
        }

        return skill;
    }

    private static int[] BuildHotKeyArrayFromRows(IReadOnlyList<CharacterHotkeyDto> hotkeys)
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

public sealed record AvatarSocialSnapshot(
    IReadOnlyDictionary<byte, string> FriendNameBySlot,
    string Teacher,
    string Student,
    string GuildName,
    int GuildRoleWire,
    string CallName = "")
{
    public static readonly AvatarSocialSnapshot Empty =
        new(new Dictionary<byte, string>(), "", "", "", 0);

    public string[] BuildFriendArray()
    {
        var friends = new string[10];
        Array.Fill(friends, "");

        foreach (var (slot, name) in FriendNameBySlot)
            if (slot < 10)
                friends[slot] = name;

        return friends;
    }
}

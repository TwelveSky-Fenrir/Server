using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Costumes;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;

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

    private const int MountSlotCount = MountPersistenceCodec.SlotCount;

    public static AvatarInfo CreateForCharacter(CharacterWorldSnapshotDto character,
        IReadOnlyList<CharacterItemSlotDto> items, AvatarSocialSnapshot? social = null,
        IReadOnlyList<CharacterSkillDto>? skills = null, IReadOnlyList<CharacterHotkeyDto>? hotkeys = null,
        IReadOnlyList<CharacterCostumeSlotDto>? costumes = null,
        IReadOnlyList<CharacterMountSlotDto>? mounts = null)
    {
        var s = social ?? AvatarSocialSnapshot.Empty;
        var (wardrobe, costumeDate, costumeExpireDate) = CostumePersistenceCodec.Hydrate(costumes ?? []);

        var garage = MountPersistenceCodec
            .Hydrate(mounts ?? [])
            .SetItem(MountPersistenceCodec.PersistedGarageSlot,
                (character.MountItemId, character.MountExpActivity, character.MountPower));
        var (animal, animalExpActivity, animalPower) = BuildMountSlotArrays(garage);

        return AvatarInfoTemplates.Zeroed with
        {
            Costume = [.. wardrobe],
            CostumeDate = [.. costumeDate],
            CostumeExpireDate = [.. costumeExpireDate],
            CostumeIndex = CostumePersistenceCodec.NormalizeIndexOnLoad(character.CostumeIndex, wardrobe),
            PetExpX2Time = character.PetExpX2Time,
            AnimalAbsorbTime = character.AnimalAbsorbTime,
            AnimalAbsorbState = character.AnimalAbsorbState,
            VisibleState = character.VisibleState,
            SpecialState = character.SpecialState,
            UseOrnament = character.UseOrnament ? 1 : 0,
            Name = character.Name,
            Tribe = character.Tribe,
            PreviousTribe = character.PreviousTribe,
            Gender = character.Gender,
            HeadType = character.HeadType,
            FaceType = character.FaceType,
            Level1 = character.Level,
            Level2 = character.Level2,
            Exp1 = (int)Math.Clamp(character.Experience, 0, MaxGeneralExperience),
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
            AutoTime = character.AutoTime,
            AutoTime2 = character.AutoTime2,
            BuffX2Time = character.BuffX2Time,
            DoubleExpTime1 = character.DoubleExpTime1,
            DoubleExpTime2 = character.DoubleExpTime2,
            SilverTime = character.SilverTime,
            GoldTime = character.GoldTime,
            DoubleKillNumTime = character.DoubleKillNumTime,
            DoubleKillExpTime = character.DoubleKillExpTime,
            DoubleKillNumTime2 = character.DoubleKillNumTime2,
            AutoBuffTime = character.AutoBuffTime,
            Premium = character.PremiumExpireUtc,
            Animal = animal,
            AnimalIndex = MountCatalog.ResolveDisplayedSlotMarker(character.MountItemId, character.MountSlotIndex),
            AnimalTime = character.MountTime,
            AnimalPower = animalPower,
            AnimalExpActivity = animalExpActivity,
            Equip = BuildEquipArrayFromRows(items),
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

        public static AvatarInfo CreateForRuntimeState(PlayerRuntimeState state, AvatarSocialSnapshot? social = null)
    {
        var s = social ?? AvatarSocialSnapshot.Empty;
        var (animal, animalExpActivity, animalPower) = BuildMountSlotArrays(state);

        return AvatarInfoTemplates.Zeroed with
        {
            Animal = animal,
            AnimalExpActivity = animalExpActivity,
            AnimalPower = animalPower,
            AnimalIndex = state.AnimalIndex,
            AnimalTime = state.AnimalTime,
            Costume = [.. state.CostumeWardrobe],
            CostumeDate = [.. state.CostumeDate],
            CostumeExpireDate = [.. state.CostumeExpireDate],
            CostumeIndex = state.CostumeIndex,
            StellarCore = [.. state.StellarCoreWardrobe],
            StellarCoreExpireDate = [.. state.StellarCoreExpireDate],
            StellarCoreIndex = state.StellarCoreIndex,
            PlayTime1 = state.PlayTime1,
            PlayTime2 = state.PlayTime2,
            PlayTime3 = state.PlayTime3,
            PetExpX2Time = state.PetExpX2Time,
            AnimalAbsorbTime = state.AnimalAbsorbTime,
            AnimalAbsorbState = state.AnimalAbsorbState,
            VisibleState = state.VisibleState,
            SpecialState = state.SpecialState,
            UseOrnament = state.UseOrnament ? 1 : 0,
            Name = state.Name,
            Tribe = state.Tribe,
            PreviousTribe = state.PreviousTribe,
            Gender = state.Gender,
            HeadType = state.HeadType,
            FaceType = state.FaceType,
            Level1 = state.Level,
            Level2 = state.Level2,
            Exp1 = (int)Math.Clamp(state.Experience, 0, MaxGeneralExperience),
            Exp2 = state.Exp2,
            Vit = state.StatVit,
            Str = state.StatStr,
            Int = state.StatInt,
            Dex = state.StatDex,
            StatPoint = state.StatPoints,
            SkillPoint = state.SkillPoints,
            Money = (int)state.Money,
            InventoryDate = state.InventoryDate,
            StoreDate = state.StoreDate,
            StoreMoney = (int)state.StoreMoney,
            BigMoney = state.BigMoney,
            Title = state.Title,
            Halo = state.Halo,
            RebirthNum = state.RebirthCount,
            Zone241Time = state.Zone241Time,
            PetBagDate = state.PetBagDate,
            WarPoint = state.WarPoint,
            BloodCoin = state.BloodCoin,
            EatLifePotion = state.EatLifePotion,
            EatManaPotion = state.EatManaPotion,
            EatStrPotion = state.EatStrPotion,
            EatDexPotion = state.EatDexPotion,
            EatElePotion = state.EatElePotion,
            ProtectForDeath = state.ProtectForDeath,
            ProtectForDestroy = state.ProtectForDestroy,
            ProtectForDestroy2 = state.ProtectForDestroy2,
            ProtectForRefine = state.ProtectForRefine,
            ProtectForCostume = state.ProtectForCostume,
            ProtectForHalo = state.ProtectForHalo,
            FightingGodForDestroy = state.FightingGodForDestroy,
            DmgBoost = state.DmgBoost,
            HPBoost = state.HPBoost,
            CriBoost = state.CriBoost,
            AutoBuffTime = state.AutoBuffTime,
            AutoBuffSkill = BuildAutoBuffSkillArray(state.AutoBuffSkill),
            AutoLifeRatio = state.AutoLifeRatio,
            AutoManaRatio = state.AutoManaRatio,
            AutoTime = state.AutoHuntPaidDayBudget,
            AutoTime2 = state.AutoHuntPaidMinuteBudget,
            AutoState = state.AutoHuntEnabled ? 1 : 0,
            AutoHunt = state.AutoHuntConfig ?? AvatarInfoTemplates.Zeroed.AutoHunt,
            ImproveItemValue = state.ImproveItemValue,
            AddItemValue = state.AddItemValue,
            HighItemValue = state.HighItemValue,
            DropItemTime = state.DropItemTime,
            BonusItemLevel = state.BonusItemLevel,
            BonusItemValue = state.BonusItemValue ? 1 : 0,
            TribeNotifyNum = state.TribeNotifyScrollCount,
            RankPoint = state.RankPoint,
            RankPointDate = state.RankPointDate,
            RankBuffType = state.RankBuffType,
            AnimalDoubleExp = state.AnimalDoubleExp,
            WarriorPill = state.WarriorPill,
            WarriorScroll = state.WarriorScroll,
            SilverTime = state.SilverTime,
            GoldTime = state.GoldTime,
            DoubleExpTime1 = state.DoubleExpTime1,
            DoubleExpTime2 = state.DoubleExpTime2,
            DoubleKillNumTime = state.DoubleKillNumTime,
            DoubleKillExpTime = state.DoubleKillExpTime,
            DoubleKillNumTime2 = state.DoubleKillNumTime2,
            Premium = state.PremiumExpireUtc,
            TeacherPoint = state.TeacherPoint,
            Friend = s.BuildFriendArray(),
            Teacher = s.Teacher,
            Student = s.Student,
            GuildName = state.GuildName,
            GuildRole = GuildRoleCodec.DbRoleToWire(state.GuildRoleDb),
            CallName = state.GuildCallName,
            PartyName = s.BuildPartyArray(),
            Equip = BuildEquipArrayFromContainer(state.Inventory.GetContainer(ContainerMatrix.Equipment)),
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
            LogoutInfo = [state.MapId, (int)state.PosX, (int)state.PosY, (int)state.PosZ, state.Life, state.Mana]
        };
    }

        public static WorldEntryAvatarProjection CreateWorldEntryProjection(PlayerRuntimeState state,
        AvatarSocialSnapshot? social = null)
    {
        var s = social ?? AvatarSocialSnapshot.Empty;

        return new WorldEntryAvatarProjection(
            state.Incarnation,
            CreateForRuntimeState(state, s),
            state.Buffs with { Buff = [.. state.Buffs.Buff] },
            CreateCurrentPose(state),
            s.PartyName);
    }

    private static ActionInfo CreateCurrentPose(PlayerRuntimeState state)
    {
        return new ActionInfo
        {
            Type = state.ActionType,
            Sort = state.ActionSort,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = [state.PetActionLocationX, state.PetActionLocationY, state.PetActionLocationZ],
            PetTargetLocation =
                [state.PetActionTargetLocationX, state.PetActionTargetLocationY, state.PetActionTargetLocationZ],
            PetFront = state.PetActionFront,
            PetSort = state.PetActionSort,
            TargetObjectSort = 0,
            TargetObjectIndex = state.ActionTargetObjectIndex,
            TargetObjectUniqueNumber = state.ActionTargetObjectUniqueNumber,
            SkillNumber = state.ActionSkillNumber,
            SkillGradeNum1 = state.ActionSkillGradeNum1,
            SkillGradeNum2 = state.ActionSkillGradeNum2,
            SkillValue = 0
        };
    }

    private static int[] BuildAutoBuffSkillArray(ImmutableArray<(int SkillId, int Grade)> skills)
    {
        var values = new int[16];

        for (var slot = 0; slot < skills.Length && slot < values.Length / 2; slot++)
        {
            values[slot * 2] = skills[slot].SkillId;
            values[slot * 2 + 1] = skills[slot].Grade;
        }

        return values;
    }

    private static (int[] ItemIds, int[] ExpActivities, int[] Powers) BuildMountSlotArrays(
        ImmutableArray<(int ItemId, int ExpActivity, int Power)> garage)
    {
        var itemIds = new int[MountSlotCount];
        var expActivities = new int[MountSlotCount];
        var powers = new int[MountSlotCount];

        for (var slot = 0; slot < MountSlotCount && slot < garage.Length; slot++)
        {
            itemIds[slot] = garage[slot].ItemId;
            expActivities[slot] = garage[slot].ExpActivity;
            powers[slot] = garage[slot].Power;
        }

        return (itemIds, expActivities, powers);
    }

    private static (int[] ItemIds, int[] ExpActivities, int[] Powers) BuildMountSlotArrays(PlayerRuntimeState state)
    {
        var itemIds = new int[MountSlotCount];
        var expActivities = new int[MountSlotCount];
        var powers = new int[MountSlotCount];

        for (var slot = 0; slot < MountSlotCount; slot++)
        {
            itemIds[slot] = state.MountGarage[slot];
            expActivities[slot] =
                MountActivityExpCodec.Pack(state.MountActivity[slot], state.MountAccumulatedExp[slot]);
            powers[slot] = MountPowerCodec.EncodeSlot(state.MountRolledAttributes, slot);
        }

        return (itemIds, expActivities, powers);
    }

    private static int[] BuildEquipArrayFromRows(IReadOnlyList<CharacterItemSlotDto> items)
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
                equip[baseIndex + 1] = Math.Clamp(item.Quantity, 0, ItemQuantityPolicy.MaxPetActivity);
                equip[baseIndex + 2] = PetItemState.Growth(item.Enchant, item.Combine, item.Refine, item.Socket);
            }
            else
            {
                equip[baseIndex + 1] = item.ExpireDate;
                equip[baseIndex + 2] = PackUpgradeBytes(item.Enchant, item.Combine, item.Refine, item.Socket);
            }
        }

        return equip;
    }

    private static int[] BuildEquipArrayFromContainer(IReadOnlyDictionary<byte, ItemStack> equipmentContainer)
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
                equip[baseIndex + 1] = PetItemState.Activity(stack);
                equip[baseIndex + 2] = PetItemState.Growth(stack);
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
            inventory[baseIndex + 1] = item.XPos;
            inventory[baseIndex + 2] = item.YPos;
            inventory[baseIndex + 3] = item.Quantity;
            inventory[baseIndex + 4] = PackUpgradeBytes(item.Enchant, item.Combine, item.Refine, item.Socket);
            inventory[baseIndex + 5] = item.Serial;
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
            inventory[baseIndex + 1] = stack.XPos;
            inventory[baseIndex + 2] = stack.YPos;
            inventory[baseIndex + 3] = stack.Quantity;
            inventory[baseIndex + 4] = PackUpgradeBytes(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
            inventory[baseIndex + 5] = stack.Serial;
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
    string CallName = "",
    IReadOnlyList<string>? PartyNames = null,
    string PartyName = "")
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

    public string[] BuildPartyArray()
    {
        var names = new string[5];
        Array.Fill(names, "");

        if (PartyNames is null)
            return names;

        for (var index = 0; index < PartyNames.Count && index < names.Length; index++)
            names[index] = PartyNames[index];

        return names;
    }
}

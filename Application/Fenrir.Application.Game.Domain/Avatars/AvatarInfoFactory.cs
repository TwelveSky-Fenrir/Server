using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Avatars;

/// <summary>
///     Independent copy of Login's AvatarInfoFactory; projects a persisted character onto AVATAR_INFO for
///     ZC_REGISTER_AVATAR_RECV.
/// </summary>
public static class AvatarInfoFactory
{
    /// <summary>
    ///     aExp1's fixed lifetime value under Fenrir's always-maximum-level creation design
    ///     (creation-persistence-full-reaudit finding, Major -- resolves the prior
    ///     zone-avatarinfo-factory-parity open question rather than leaving it unset). Every Fenrir
    ///     character is created already at the general-level cap (Level1 = MAX_LIMIT_LEVEL_NUM = 145) with
    ///     aExp1 pinned at MAX_NUMBER_SIZE in that very same creation assignment
    ///     (Server/ts25login/S04_MyWork02.cpp:1100-1106), and MyUtil::ProcessForExperience never reassigns
    ///     aExp1 once it already equals MAX_NUMBER_SIZE (Server/ts25zone/S07_MyGame03.cpp:196-198, :341) --
    ///     all further general-experience gains route to aExp2/ProcessForExperience2 instead. General level
    ///     can never regress below the cap either (TribeActionService.RebirthAsync's own precondition
    ///     requires Level already == the cap and never writes to it), so aExp1 equals this constant for a
    ///     character's entire observed lifetime, at both creation and every later avatar-info projection
    ///     (world entry / zone-to-zone transfer) alike. See Server/Header/Protocol/DEFINE.h:365
    ///     (MAX_NUMBER_SIZE = 2,000,000,000). This is distinct from -- and does not resolve -- the separate
    ///     open question on whether the "aExp1+aExp2 combined" framing of Characters.Experience is itself
    ///     the right column-comment model; see that field's own doc comment for that unrelated finding.
    /// </summary>
    private const int MaxGeneralExperience = 2_000_000_000;

    // wAvatar.aInventory[2][64][6] -- ServerDocs/12_ts25zone/06_MyWork03_Items_Equipement_Craft_Quetes.md:
    // 115-117 ("Format d'inventaire, 6 entiers par slot... [0] ID objet, [3] quantité/durabilité, [4] valeur
    // d'amélioration/option, [5] réservé") ; page/slot bounds from Database/Tables/game/CharacterItems.sql's
    // own CK_CharacterItems_ContainerSlot (Container 0/1, Slot <= 63) ; total wire size cross-checked against
    // ServerDocs/19_Header_Lib/06_FunctionH_Utilitaires_XOR.md:276's MAX_INVENTORY_PAGE_NUM *
    // MAX_INVENTORY_SLOT_NUM * MAX_INVENTORY_VALUE_NUM = 2*64*6 = 768, matching AvatarInfo.Inventory's own
    // FixedArray(768). [1]/[2] are NOT populated: the cited doc only pins [0]/[3]/[4]/[5], leaving 2 of
    // CharacterItemSlotDto's own remaining fields (ExpireDate/Serial) as the only plausible candidates for
    // the other 2 ints, but WHICH of [1]/[2] holds which is not established by any citation available to this
    // pass -- left at wire-zero rather than guessed (open question, needs a cpp-protocol-header-analyst pass
    // over Server/Header/Protocol/STRUCT.h's exact AVATAR_INFO field layout to close).
    private const int InventoryPageCount = 2;
    private const int InventorySlotsPerPage = 64;
    private const int InventoryWireIntsPerSlot = 6;

    // wAvatar.aStoreItem[2][28][4] -- same ItemId/ExpireDate/packed-upgrade-byte/unused convention as
    // aEquip[13][4] above (ServerDocs/19_Header_Lib/06_FunctionH_Utilitaires_XOR.md:277's
    // MAX_STORE_ITEM_PAGE_NUM * MAX_STORE_ITEM_SLOT_NUM * MAX_STORE_ITEM_VALUE_NUM = 2*28*4 = 224, matching
    // AvatarInfo.StoreItem's own FixedArray(224) and confirming MAX_STORE_ITEM_VALUE_NUM = 4, the same 4 as
    // MAX_EQUIP_VALUE_NUM). Page/slot bounds from CharacterItems.sql's CK_CharacterItems_ContainerSlot
    // (Container 3/4, Slot <= 27). "Store" here is the persisted account storage/warehouse container
    // (ContainerMatrix.StorePage0/1, moved between via StoreItemTransferPolicy) -- distinct from the
    // ephemeral, never-persisted player-shop-stall (PShop) concept, which genuinely has no queryable state at
    // this point and is correctly out of scope.
    private const int StorePageCount = 2;
    private const int StoreSlotsPerPage = 28;
    private const int StoreWireIntsPerSlot = 4;

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

    /// <summary>
    ///     <paramref name="skills" />/<paramref name="hotkeys" /> default to null (treated as empty) rather than
    ///     being required, so every pre-existing caller that only cared about identity/stat/equipment fields
    ///     keeps compiling -- see BuildSkillArrayFromRows/BuildHotKeyArrayFromRows for the population itself.
    /// </summary>
    public static AvatarInfo CreateForCharacter(CharacterWorldSnapshotDto character,
        IReadOnlyList<CharacterItemSlotDto> items, AvatarSocialSnapshot? social = null,
        IReadOnlyList<CharacterSkillDto>? skills = null, IReadOnlyList<CharacterHotkeyDto>? hotkeys = null)
    {
        var s = social ?? AvatarSocialSnapshot.Empty;

        return AvatarInfoTemplates.Zeroed with
        {
            Name = character.Name,
            Tribe = character.Tribe,
            // creation-persistence-full-reaudit finding (Major): the Noble Dragon/Royal Serpent/Grand Tiger
            // starter-kit template (0-2), genuinely independent of Tribe -- previously left at Zeroed's 0
            // default on every ordinary (non-creation) world entry even though CharacterWorldSnapshotDto
            // already carries the correct persisted value (Migrations/018_character_previous_tribe_and_mount_
            // readpath.sql) and CreateAvatarService's own creation-time response overlay already sets it
            // correctly (Server/ts25login/S04_MyWork02.cpp:743). Same field EnterWorldService's own
            // ObjectForAvatar.PreviousTribe already reflects for the self-spawn broadcast -- this closes the
            // matching gap on AVATAR_INFO itself.
            PreviousTribe = character.PreviousTribe,
            Gender = character.Gender,
            HeadType = character.HeadType,
            FaceType = character.FaceType,
            Level1 = character.Level,
            Level2 = character.Level2,
            // aExp1 (creation-persistence-full-reaudit finding, Major -- resolves the prior
            // zone-avatarinfo-factory-parity open question): a fixed constant for every character's entire
            // lifetime under Fenrir's always-maximum-level creation design, NOT a value decomposed from
            // CharacterWorldSnapshotDto.Experience. See MaxGeneralExperience's own remarks for the full
            // citation chain.
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
            // aZone241Time (Rebirth-advancement contract's Path B, "Max Rebirth" aZone241Time += 10): previously
            // left at Zeroed's 0 default even though CharacterWorldSnapshotDto already carries the persisted
            // value (Migrations/041_character_rebirth_zone241_time.sql) -- same "reflect the currently-
            // persisted value, don't recompute it" precedent as Level2/Exp2/RebirthNum above.
            Zone241Time = character.Zone241Time,
            // aPetBagDate (C8 pet-bag-entitlement confirmation-pass follow-up): previously left at Zeroed's 0
            // default even though CharacterWorldSnapshotDto now carries the persisted value
            // (Migrations/047_characters_petbagdate_column.sql) -- same "reflect the currently-persisted
            // value, don't recompute it" precedent as Zone241Time immediately above. See
            // PlayerRuntimeState.PetBagDate's own remarks for the full wiring/citation trail.
            PetBagDate = character.PetBagDate,
            // aWarPoint (Migrations/041_characters_warpoint_currency.sql's own flagged follow-up): previously
            // left at Zeroed's 0 default even though CharacterWorldSnapshotDto now carries the persisted
            // value -- same "reflect the currently-persisted value, don't recompute it" precedent as
            // Zone241Time/PetBagDate immediately above. See PlayerRuntimeState.WarPoint's own remarks.
            WarPoint = character.WarPoint,
            // Stat/elixir-potion lifetime counters (item-usage-consumables finding, Critical): previously
            // left at Zeroed's 0 default on every world entry even though CharacterWorldSnapshotDto already
            // carries the persisted values -- same "reflect the currently-persisted value, don't recompute
            // it" precedent as Level2/Exp2/RebirthNum above. See PlayerRuntimeState.EatLifePotion's own
            // remarks for the full wiring.
            EatLifePotion = character.EatLifePotion,
            EatManaPotion = character.EatManaPotion,
            EatStrPotion = character.EatStrPotion,
            EatDexPotion = character.EatDexPotion,
            EatElePotion = character.EatElePotion,
            // Migrations/027_character_autotime2_grant.sql (closes the prior round's open aAutoTime2
            // question): aAutoTime2, the free auto-hunt minute allowance (Server/ts25login/S04_MyWork02.cpp:
            // 888 grants 1440 == 24h of minutes at creation; Server/ts25zone/S07_MyGame04.cpp:787-823
            // decrements it by 1 per elapsed real minute while auto-hunt is enabled).
            AutoTime2 = character.AutoTime2,
            // creation-persistence-full-reaudit finding (Major): DoubleExpTime1/DoubleExpTime2/AutoBuffTime/
            // Premium were previously left at Zeroed's 0 default here on every ordinary (non-creation) world
            // entry -- these flip from correct to zero the instant a returning character actually enters the
            // world, even though CreateAvatarService's own creation-time response overlay already sets every
            // one of them correctly (Server/ts25login/S04_MyWork02.cpp:885-892 for DoubleExpTime1/DoubleExpTime2/
            // AutoBuffTime, :895-905 for Premium) and CharacterWorldSnapshotDto already carries the persisted
            // values back on every read (Database/Migrations/018_character_previous_tribe_and_mount_readpath.sql).
            // Reflected verbatim below, the same "reflect the currently-persisted value on every entry, don't
            // recompute it" precedent already established for Level2/Exp2/StatPoint/SkillPoint/pet-growth above
            // -- in particular, Premium is NOT re-run through legacy's creation-time "extend by one day if
            // already expired" computation, since no citation establishes whether legacy's own per-login flow
            // re-runs that (an open question flagged for a cpp-zone-gameplay-analyst re-check, not resolved
            // here by guessing). ProtectForDeath has the identical mapping gap but is deliberately NOT part of
            // this fix -- flagged only as a related follow-up observation, not one of the fields this pass
            // closes.
            DoubleExpTime1 = character.DoubleExpTime1,
            DoubleExpTime2 = character.DoubleExpTime2,
            AutoBuffTime = character.AutoBuffTime,
            Premium = character.PremiumExpireUtc,
            // Active mount grant (same finding as above): MountItemId/MountExpActivity/MountPower/
            // MountSlotIndex/MountTime were durably persisted by Migrations/015_starter_kit_elite_grant.sql and
            // read back by CharacterWorldSnapshotDto, but never projected onto AVATAR_INFO's own Animal/
            // AnimalIndex/AnimalTime/AnimalPower/AnimalExpActivity fields outside of CreateAvatarService's
            // one-time creation-response overlay (Server/ts25login/S04_MyWork02.cpp:1174-1179 for the legacy
            // creation-time grant this mirrors). See BuildSingleMountSlotArray's own remarks for why the
            // character's own persisted MountSlotIndex, not a hardcoded slot, drives which of the 10 wire
            // slots gets the value.
            Animal = BuildSingleMountSlotArray(character.MountItemId, character.MountSlotIndex),
            AnimalIndex = character.MountSlotIndex,
            AnimalTime = character.MountTime,
            AnimalPower = BuildSingleMountSlotArray(character.MountPower, character.MountSlotIndex),
            AnimalExpActivity = BuildSingleMountSlotArray(character.MountExpActivity, character.MountSlotIndex),
            Equip = BuildEquipArrayFromRows(items, character.PetGrowth, character.PetActivity),
            // Inventory/StoreItem/Skill/HotKey (zone-avatarinfo-factory-parity finding): previously left at
            // Zeroed's all-zero default even though RS1/RS2/RS3 of usp_Character_GetForWorldEntry already
            // carry every row needed -- items/skills/hotkeys are the exact same collections EnterWorldService
            // reads for the equipment container / Zone.Post(PlayerEnterData) below, just re-projected onto
            // these four wire arrays too, the same "no second round trip" posture Equip above already uses.
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

    /// <summary>
    ///     mapId/pos are the just-resolved destination -- <paramref name="state" /> itself still holds the source zone's
    ///     position at call time.
    /// </summary>
    public static AvatarInfo CreateForRuntimeState(PlayerRuntimeState state, short mapId, float posX, float posY,
        float posZ)
    {
        return AvatarInfoTemplates.Zeroed with
        {
            Name = state.Name,
            Tribe = state.Tribe,
            // creation-persistence-full-reaudit finding (Major), zone-transfer half: unlike CreateForCharacter's
            // matching gap above, PlayerRuntimeState already carries PreviousTribe in memory (see its own
            // remarks in PlayerRuntimeState.Misc.cs), so this is a pure mapping fix, not a missing-data one --
            // wired here the same way.
            PreviousTribe = state.PreviousTribe,
            Gender = state.Gender,
            HeadType = state.HeadType,
            FaceType = state.FaceType,
            Level1 = state.Level,
            Level2 = state.Level2,
            // aExp1 -- same lifetime-constant rule as CreateForCharacter above, not a decomposition of the
            // combined PlayerRuntimeState.Experience counter. See MaxGeneralExperience's own remarks for the
            // full citation chain.
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
            // aZone241Time -- same wiring/precedent as CreateForCharacter above, sourced from the in-memory
            // PlayerRuntimeState field (already carried through a zone-transfer by ZoneTransfer.CreateEnterData)
            // rather than a fresh DB read.
            Zone241Time = state.Zone241Time,
            // aPetBagDate -- same wiring/precedent as CreateForCharacter above, sourced from the in-memory
            // PlayerRuntimeState field (already carried through a zone-transfer by ZoneTransfer.CreateEnterData)
            // rather than a fresh DB read.
            PetBagDate = state.PetBagDate,
            // aWarPoint -- same wiring/precedent as CreateForCharacter above, sourced from the in-memory
            // PlayerRuntimeState field (already carried through a zone-transfer by ZoneTransfer.CreateEnterData)
            // rather than a fresh DB read.
            WarPoint = state.WarPoint,
            // Stat/elixir-potion lifetime counters -- same wiring as CreateForCharacter above, sourced from
            // the in-memory PlayerRuntimeState fields (already carried through a zone-transfer by
            // ZoneTransfer.CreateEnterData) rather than a fresh DB read.
            EatLifePotion = state.EatLifePotion,
            EatManaPotion = state.EatManaPotion,
            EatStrPotion = state.EatStrPotion,
            EatDexPotion = state.EatDexPotion,
            EatElePotion = state.EatElePotion,
            // creation-persistence-full-reaudit finding (Major), zone-transfer half continued: AutoBuffTime is
            // the other field PlayerRuntimeState already carries in memory (PlayerRuntimeState.Automation.cs),
            // so it is wired here too. The active-mount grant, DoubleExpTime1/DoubleExpTime2, and Premium are
            // deliberately NOT added here, unlike CreateForCharacter above -- PlayerRuntimeState has no field
            // for any of the three at all (no matching in-memory state found by search), so this is a
            // missing-data gap one layer deeper than a mapping fix can close; whether PlayerRuntimeState should
            // grow those fields is a separate PlayerRuntimeState-ownership decision (see
            // fenrir-realtime-simulation-architect), not something this factory can resolve on its own.
            AutoBuffTime = state.AutoBuffTime,
            GuildName = state.GuildName,
            GuildRole = GuildRoleCodec.DbRoleToWire(state.GuildRoleDb),
            CallName = state.GuildCallName,
            Equip = BuildEquipArrayFromContainer(state.Inventory.GetContainer(ContainerMatrix.Equipment),
                state.PetGrowth, state.PetActivity),
            // Inventory/StoreItem/Skill (same zone-avatarinfo-factory-parity finding as CreateForCharacter
            // above): state.Inventory already tracks every container (InventoryState.GetContainer is generic
            // over all 5 legacy container ids, not just Equipment) and state.LearnedSkills already tracks
            // every skill slot in memory -- both are re-projected here the same "no second round trip" way
            // Equip already is, instead of the previous all-zero default.
            Inventory = BuildInventoryArrayFromContainers(
                state.Inventory.GetContainer(ContainerMatrix.InventoryPage0),
                state.Inventory.GetContainer(ContainerMatrix.InventoryPage1)),
            StoreItem = BuildStoreItemArrayFromContainers(
                state.Inventory.GetContainer(ContainerMatrix.StorePage0),
                state.Inventory.GetContainer(ContainerMatrix.StorePage1)),
            Skill = BuildSkillArrayFromLearnedSkills(state.LearnedSkills),
            // HotKey deliberately NOT populated here, unlike CreateForCharacter's BuildHotKeyArrayFromRows:
            // PlayerRuntimeState has no Hotkey field at all (only Inventory/LearnedSkills are carried in
            // memory -- see this type's own declarations), so an intra-shard zone-to-zone transfer (this
            // method's only caller, ZoneMoveService) has no in-memory source to project from. Left at
            // Zeroed's 0 default rather than guessed; closing this needs PlayerRuntimeState to actually carry
            // hotkey state (seeded from EnterWorldService's bundle.Hotkeys via PlayerEnterData, mirrored the
            // way Inventory/LearnedSkills already are), which is a PlayerRuntimeState/Zone-ownership change
            // outside this factory's scope (see fenrir-realtime-simulation-architect).
            QuestInfo =
            [
                state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort, state.QuestTargetPhase,
                state.QuestKillCounter
            ],
            LogoutInfo = [mapId, (int)posX, (int)posY, (int)posZ, state.Life, state.Mana]
        };
    }

    /// <summary>
    ///     Places <paramref name="value" /> at <paramref name="slotIndex" /> of an otherwise-zeroed 10-slot array --
    ///     the shared shape AVATAR_INFO's Animal/AnimalPower/AnimalExpActivity arrays all use. Mirrors
    ///     Login's own CreateAvatarService.SingleMountSlotArray (Application/Fenrir.Application.Login.Services/
    ///     CreateAvatar/CreateAvatarService.cs), but keyed off the character's own persisted MountSlotIndex
    ///     instead of a creation-time constant, since a returning character's active mount slot is itself a
    ///     persisted value, not always slot 0.
    /// </summary>
    /// <remarks>
    ///     An out-of-range persisted <paramref name="slotIndex" /> (defensive only -- every write site currently
    ///     ever grants slot 0, see Migrations/015_starter_kit_elite_grant.sql) is dropped rather than throwing --
    ///     corrupt/legacy-edge data here must never crash world entry.
    /// </remarks>
    private static int[] BuildSingleMountSlotArray(int value, int slotIndex)
    {
        var slots = new int[10];

        if (slotIndex is >= 0 and < 10)
            slots[slotIndex] = value;

        return slots;
    }

    /// <summary>
    ///     <c>aEquip[13][4]</c>'s 4th int per slot is unknown/unmapped -- left at wire-zero rather than guessed.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1131-1135 -- the pet slot (<see cref="PetSlots.EquipmentSlot" />)
    ///     is the one exception to the generic ItemId/ExpireDate/packed-upgrade-byte mapping every other slot
    ///     uses: its 2nd/3rd wire ints are <paramref name="petActivity" />/<paramref name="petGrowth" /> as plain
    ///     values, since those two live on the character record itself (<see cref="PetGrowthCalculator" /> reads
    ///     the same two fields for combat-stat math), not on the pet's own CharacterItems row.
    /// </remarks>
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

    /// <summary>
    ///     Same pet-slot exception as <see cref="BuildEquipArrayFromRows" /> above, applied to the in-memory equipment
    ///     container instead of freshly-read rows.
    /// </summary>
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

    /// <summary>
    ///     Bit pattern is identical whether read as signed or unsigned, so packing unsigned bytes reproduces the legacy's
    ///     signed-char wire int.
    /// </summary>
    private static int PackUpgradeBytes(byte enchant, byte combine, byte refine, byte socket)
    {
        return enchant | (combine << 8) | (refine << 16) | (socket << 24);
    }

    /// <summary>
    ///     See <see cref="InventoryPageCount" />'s own remarks for the exact per-slot field mapping and its one open
    ///     question.
    /// </summary>
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

    /// <summary>
    ///     Same mapping as <see cref="BuildInventoryArrayFromRows" />, applied to the in-memory per-page containers
    ///     instead of freshly-read rows.
    /// </summary>
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

    /// <summary>See <see cref="StorePageCount" />'s own remarks for the exact per-slot field mapping.</summary>
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

    /// <summary>
    ///     Same mapping as <see cref="BuildStoreItemArrayFromRows" />, applied to the in-memory per-page containers
    ///     instead of freshly-read rows.
    /// </summary>
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

    /// <summary>See <see cref="SkillSlotCount" />'s own remarks for the citation.</summary>
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

    /// <summary>
    ///     Same mapping as <see cref="BuildSkillArrayFromRows" />, applied to the in-memory learned-skill map instead of
    ///     freshly-read rows.
    /// </summary>
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

    /// <summary>
    ///     See <see cref="HotkeyPageCount" />'s own remarks for the citation. No in-memory-container
    ///     counterpart exists yet -- see CreateForRuntimeState's own HotKey remarks for why.
    /// </summary>
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

/// <summary>
///     PartyName/DuelState aren't modeled here: neither party membership nor a duel can exist at login time, so both
///     stay blank.
/// </summary>
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

    /// <summary>Slots are client-chosen, so gaps in the sparse map are normal -- unfilled slots stay empty string.</summary>
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

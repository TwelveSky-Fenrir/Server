-- Adds persistent X/Y grid-position coordinates for personal-inventory bag items (game.CharacterItems.
-- Container IN (0, 1), InventoryPage0/InventoryPage1 -- equip/store/trade/pet-bag never had X/Y in legacy
-- and are not touched here). Two write paths only: inventory-to-inventory move and ground-item pickup.
--
-- WHY A V2 TYPE INSTEAD OF ALTERING tvp_CharacterItemSlot: same reasoning as Schemas/Types/game/
-- tvp_AccountVaultItemSlotV2.sql -- a TABLE type cannot be ALTERed and cannot be DROPped while any
-- procedure still takes it as a parameter, and ~22 other write paths in ItemStack.ToTvp/usp_CharacterItems_
-- ReplaceContainer(TwoContainers) stay on V1 deliberately, out of scope for this change. V2 is a strict
-- superset of V1's column list (identical prefix, XPos/YPos appended), so ReplaceContainerV2/
-- ReplaceTwoContainersV2 are the V1 bodies unchanged except for the widened TVP type and INSERT list.
--
-- 1a. New columns, DEFAULT 0 so every pre-existing row (and every V1 write path that keeps inserting
--     through the un-widened column list) stays valid against the CHECK below without a data backfill.
ALTER TABLE game.CharacterItems
    ADD XPos TINYINT NOT NULL CONSTRAINT DF_CharacterItems_XPos DEFAULT 0,
        YPos TINYINT NOT NULL CONSTRAINT DF_CharacterItems_YPos DEFAULT 0;
GO

ALTER TABLE game.CharacterItems WITH CHECK
    ADD CONSTRAINT CK_CharacterItems_BagPosition CHECK (XPos BETWEEN 0 AND 7 AND YPos BETWEEN 0 AND 7);
GO

-- 1b. V2 TVP: exact column-for-column copy of tvp_CharacterItemSlot.sql, XPos/YPos appended.
CREATE TYPE game.tvp_CharacterItemSlotV2 AS TABLE
(
    Slot       TINYINT NOT NULL,
    ItemId     INT     NOT NULL,
    Quantity   INT     NOT NULL,
    Enchant    TINYINT NOT NULL,
    Combine    TINYINT NOT NULL,
    Refine     TINYINT NOT NULL,
    Socket     TINYINT NOT NULL,
    SocketGem1 INT     NOT NULL,
    SocketGem2 INT     NOT NULL,
    SocketGem3 INT     NOT NULL,
    ExpireDate INT     NOT NULL,
    Serial     INT     NOT NULL,
    XPos       TINYINT NOT NULL,
    YPos       TINYINT NOT NULL
);
GO

-- 1c. V2 siblings of usp_CharacterItems_ReplaceContainer / usp_CharacterItems_ReplaceTwoContainers --
--     same transaction shape, same XACT_ABORT ON, same DELETE-then-INSERT, INSERT list widened by XPos/YPos.
CREATE PROCEDURE game.usp_CharacterItems_ReplaceContainerV2 @CharacterId INT,
                                                            @Container TINYINT,
                                                            @Items game.tvp_CharacterItemSlotV2 READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                     Enchant, Combine, Refine, Socket,
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
    SELECT @CharacterId,
           @Container,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial,
           XPos,
           YPos
    FROM @Items;

    COMMIT TRANSACTION;
END;
GO

-- ContainerA=ContainerB guard reuses the V1 wording but needs its own ErrorCatalog row: 50260 already
-- means the same guard for usp_CharacterItems_ReplaceTwoContainers, and the collision rule in
-- admin.ErrorCatalog's own header only allows reusing a number when every thrower means the identical
-- failure for the identical procedure family -- V1 and V2 are two different procedures, so this gets the
-- next free number instead (registered by Migrations/Seed/admin/030_error_catalog_characteritems_v2.sql).
CREATE PROCEDURE game.usp_CharacterItems_ReplaceTwoContainersV2 @CharacterId INT,
                                                                @ContainerA TINYINT,
                                                                @ItemsA game.tvp_CharacterItemSlotV2 READONLY,
                                                                @ContainerB TINYINT,
                                                                @ItemsB game.tvp_CharacterItemSlotV2 READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        @ContainerA = @ContainerB
        THROW 50361, N'ContainerA and ContainerB must differ -- use usp_CharacterItems_ReplaceContainerV2 for a same-container move.', 1;

    BEGIN
        TRANSACTION;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @ContainerA;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                     Enchant, Combine, Refine, Socket,
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
    SELECT @CharacterId,
           @ContainerA,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial,
           XPos,
           YPos
    FROM @ItemsA;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @ContainerB;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                     Enchant, Combine, Refine, Socket,
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
    SELECT @CharacterId,
           @ContainerB,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial,
           XPos,
           YPos
    FROM @ItemsB;

    COMMIT TRANSACTION;
END;
GO

-- 1d. usp_Character_GetForWorldEntry, reproduced verbatim from Migrations/042_character_progress_tvp_full_
--     reconciliation.sql (the last script to CREATE OR ALTER it, per _manifest.txt) -- only the
--     CharacterItems result set changes, widened to project XPos, YPos. Every other result set (RS0,
--     Skills, Hotkeys, Buffs) is untouched.
CREATE OR ALTER PROCEDURE game.usp_Character_GetForWorldEntry @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.CharacterId,
           c.AccountId,
           c.Slot,
           c.Name,
           c.Tribe,
           c.Gender,
           c.HeadType,
           c.FaceType,
           c.Level,
           c.MapId,
           c.PosX,
           c.PosY,
           c.PosZ,
           c.Heading,
           c.Life,
           c.MaxLife,
           c.Mana,
           c.MaxMana,
           c.FlushSequence,
           c.Experience,
           c.Level2,
           c.StatVit,
           c.StatStr,
           c.StatInt,
           c.StatDex,
           c.StatPoints,
           c.SkillPoints,
           c.Money,
           c.BigMoney,
           c.StoreMoney,
           c.BigStoreMoney,
           c.RebirthCount,
           c.Title,
           c.Halo,
           c.ContributionPoints,
           c.EatLifePotion,
           c.EatManaPotion,
           c.EatStrPotion,
           c.EatDexPotion,
           c.EatElePotion,
           c.ProtectForDeath,
           c.ProtectForDestroy,
           c.DoubleExpTime1,
           c.DoubleExpTime2,
           c.DropItemTime,
           c.InventoryDate,
           c.StoreDate,
           ISNULL(q.StepPermanent, 0) AS QuestStepPermanent,
           ISNULL(q.ActiveQuestId, 0) AS QuestActiveId,
           ISNULL(q.QSort, 0)         AS QuestSort,
           ISNULL(q.TargetPhase, 0)   AS QuestTargetPhase,
           ISNULL(q.KillCounter, 0)   AS QuestKillCounter,
           c.JoinWar,
           c.MissionKillOtherTribe,
           c.MissionKillMonster,
           c.MissionPlayTime,
           c.AutoHuntEnabled,
           c.AutoHuntConfig,
           c.AutoLifeRatio,
           c.AutoManaRatio,
           c.PetGrowth,
           c.PetActivity,
           c.TeacherPoint,
           c.AutoBuffTime,
           c.PremiumExpireUtc,
           c.Exp2,
           c.PreviousTribe,
           c.MountItemId,
           c.MountExpActivity,
           c.MountPower,
           c.MountSlotIndex,
           c.MountTime,
           c.AutoTime2,
           c.Zone241Time,
           c.PetBagDate,
           c.WarPoint,
           c.M15PetLuckyBoxPity,
           c.VisibleState,
           c.SpecialState,
           c.UseOrnament,
           c.BloodCoin,
           c.PetExpX2Time,
           c.AnimalAbsorbTime,
           c.AnimalAbsorbState,
           c.CostumeIndex,
           c.ProtectForHalo,
           c.BonusItemLevel,
           c.BonusItemValue,
           c.TribeNotifyScrollCount,
           c.TribeFourReturnAllowance,
           c.BottleSlots,
           c.DrunkBottleIndex,
           c.AutoBuffSkill,
           c.RankPointDate,
           c.RankBuffType,
           c.AutoTime,
           c.BuffX2Time,
           c.ImproveItemValue,
           c.AddItemValue,
           c.HighItemValue,
           c.TaiyanKeyTimer,
           c.RankPoint,
           c.CloakLuckyBoxPity,
           c.CloakVariantBoxPity,
           c.MountVariantBoxPity,
           c.ProtectForRefine,
           c.ProtectForCostume,
           c.ProtectForDestroy2,
           c.LodRounds,
           c.StellarCoreExpireDate,
           c.EliteDungeonTime,
           c.DungeonKeyTime,
           c.IvyHallTicketTime,
           c.ScrollOfSeekersTime,
           c.FightingGodForDestroy,
           c.PlayTime1,
           c.PlayTime3,
           c.HsbStoneRewardClaimed,
           c.TowerCpMilestoneCounter,
           c.WarriorPill,
           c.WarriorScroll
    FROM game.Characters AS c
             LEFT JOIN game.CharacterQuests AS q
                       ON q.CharacterId = c.CharacterId
    WHERE c.CharacterId = @CharacterId;

    SELECT Container,
           Slot,
           ItemId,
           CAST(Quantity AS INT) AS Quantity, -- game.CharacterItems.Quantity is SMALLINT; widen back to INT
           Enchant,                           -- here so CharacterItemSlotDto's existing int-typed ctor param
           Combine,                           -- keeps reading it via SqlDataReader.GetInt32 without an
           Refine,                            -- InvalidCastException (see CharacterItems.sql's own comment)
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial,
           XPos,
           YPos
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
    ORDER BY Container, Slot;

    SELECT SlotIndex,
           SkillId,
           Grade
    FROM game.CharacterSkills
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;

    SELECT Page,
           KeyIndex,
           Sort,
           Value1,
           Value2
    FROM game.CharacterHotkeys
    WHERE CharacterId = @CharacterId
    ORDER BY Page, KeyIndex;

    SELECT SlotIndex,
           Value,
           RemainingLegacyTicks
    FROM game.CharacterBuffs
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;
END;
GO

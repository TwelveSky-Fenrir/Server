-- Contract: no parameters -> 2 result sets, called once at GameServer boot to populate an in-memory
-- cache (world.* is read-mostly reference data, never queried per-game-tick -- one round trip, one
-- proc, no native compilation needed). Both sets are ordered by ItemId (RS1 then by SlotIndex) so the
-- caller can zip them back together into one ItemId -> Item+BonusSkills shape without a join that
-- would otherwise duplicate the parent row per populated bonus-skill slot.
--   RS0: one row per world.Items (34,353 rows)                -- ItemId, Name, ..., CapeInfo3
--   RS1: one row per populated world.ItemBonusSkills slot      -- ItemId, SlotIndex, SkillId, Value
-- Idempotent: yes (read-only). Errors: none.
CREATE PROCEDURE world.usp_Item_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ItemId,
       Name,
       Description1,
       Description2,
       Description3,
       Type,
       Sort,
       DataNumber2D,
       DataNumber3D,
       AddDataNumber3D,
       Level,
       MartialLevel,
       EquipInfo1,
       EquipInfo2,
       BuyCost,
       SellCost,
       BuyCost2,
       LevelLimit,
       MartialLevelLimit,
       CheckMonsterDrop,
       CheckNpcSell,
       CheckNpcShop,
       CheckAvatarDrop,
       CheckAvatarTrade,
       CheckAvatarShop,
       CheckImprove,
       CheckHighImprove,
       CheckHighItem,
       CheckLowItem,
       CheckExchange,
       CheckSetItem,
       CheckDateItem,
       Strength,
       Dexterity,
       Vitality,
       Intelligent,
       Luck,
       AttackPower,
       DefensePower,
       AttackSuccess,
       AttackBlock,
       ElementAttackPower,
       ElementDefensePower,
       Critical,
       PotionType1,
       PotionType2,
       GainSkillNumber,
       LastAttackBonusInfo1,
       LastAttackBonusInfo2,
       CapeInfo1,
       CapeInfo2,
       CapeInfo3
FROM world.Items
ORDER BY ItemId;

SELECT ItemId,
       SlotIndex,
       SkillId,
       Value
FROM world.ItemBonusSkills
ORDER BY ItemId, SlotIndex;
END;

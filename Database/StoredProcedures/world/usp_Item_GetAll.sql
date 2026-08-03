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

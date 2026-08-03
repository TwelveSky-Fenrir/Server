CREATE PROCEDURE world.usp_Monster_GetDrops
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT MonsterId, DropRate, MinAmount, MaxAmount
    FROM world.MonsterDropMoney
    ORDER BY MonsterId;

    SELECT MonsterId, SlotIndex, DropRate, PotionItemId
    FROM world.MonsterDropPotions
    ORDER BY MonsterId, SlotIndex;

    SELECT MonsterId, SlotIndex, DropRate, ItemId
    FROM world.MonsterDropExtraItems
    ORDER BY MonsterId, SlotIndex;

    SELECT MonsterId, CategoryIndex, Value
    FROM world.MonsterDropCategoryRates
    ORDER BY MonsterId, CategoryIndex;

    SELECT MonsterId, DropRate, QuestItemId
    FROM world.MonsterDropQuestItems
    ORDER BY MonsterId;
END;

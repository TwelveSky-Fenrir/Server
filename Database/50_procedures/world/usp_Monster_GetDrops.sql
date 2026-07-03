-- Contract: no parameters -> 5 result sets, one per drop child table, in this fixed order:
--   RS0 world.MonsterDropMoney         { MonsterId INT, DropRate INT, MinAmount INT, MaxAmount INT }        -- 1139 rows (dense, 1 per monster)
--   RS1 world.MonsterDropPotions       { MonsterId INT, SlotIndex TINYINT, DropRate INT, PotionItemId INT } -- 274 rows (populated slots only)
--   RS2 world.MonsterDropExtraItems    { MonsterId INT, SlotIndex TINYINT, DropRate INT, ItemId INT NULL }  -- 13686 rows (populated slots only)
--   RS3 world.MonsterDropCategoryRates { MonsterId INT, CategoryIndex TINYINT, Value INT }                  -- 2130 rows (populated slots only)
--   RS4 world.MonsterDropQuestItems    { MonsterId INT, DropRate INT, QuestItemId INT }                     -- 24 rows (at most 1 per monster)
-- Called once at GameServer boot alongside usp_Monster_GetAll (kept as a separate call rather than
-- joined into it: joining any of these fan-out child tables into the base monster row would multiply
-- the base row per drop slot -- the caller reads each result set via ADO.NET NextResult() into 5
-- separate per-MonsterId lookup structures instead).
-- Idempotent: yes (read-only). Errors: none.
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

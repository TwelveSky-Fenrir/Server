-- Contract: no parameters -> RS0, one row per world.QuestRewards row (1434 rows, only the populated reward
-- slots -- see that table's header comment for the RewardType=1 "none" sentinel that is never stored). Called
-- once at GameServer boot alongside world.usp_Quest_GetAll to build a per-QuestId reward lookup.
-- Ordered by QuestId, SlotIndex for deterministic, cache-friendly load order.
-- Idempotent: yes (read-only). Errors: none.
CREATE PROCEDURE world.usp_QuestReward_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT QuestId, SlotIndex, RewardType, ItemId, Amount
FROM world.QuestRewards
ORDER BY QuestId, SlotIndex;
END;

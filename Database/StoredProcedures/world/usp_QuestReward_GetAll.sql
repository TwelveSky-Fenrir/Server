CREATE PROCEDURE world.usp_QuestReward_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT QuestId, SlotIndex, RewardType, ItemId, Amount
FROM world.QuestRewards
ORDER BY QuestId, SlotIndex;
END;

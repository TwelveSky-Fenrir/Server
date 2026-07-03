-- Contract: @QuestId INT -> three result sets for a single quest, in this order: RS0 zero or one row (the
-- world.Quests row itself); RS1 its world.QuestRewards rows; RS2 its world.QuestSpeeches rows.
-- Tooling/debugging helper only -- GameServer itself always loads the full set via world.usp_Quest_GetAll
-- plus world.usp_QuestReward_GetAll/world.usp_QuestSpeech_GetAll once at boot, never a single quest per
-- request. Bundled as one multi-result-set call rather than three separate procedures purely because
-- "show me everything about this one quest" is the actual tooling use case.
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_Quest_GetById @QuestId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT QuestId,
       Subject,
       Category,
       Step,
       Level,
       Type,
       Sort,
       SummonZoneNumber,
       SummonPosX,
       SummonPosY,
       SummonPosZ,
       StartNPCNumber,
       KeyNpcNumber1,
       KeyNpcNumber2,
       KeyNpcNumber3,
       KeyNpcNumber4,
       KeyNpcNumber5,
       EndNPCNumber,
       Solution1,
       Solution2,
       Solution3,
       Solution4,
       NextIndex
FROM world.Quests
WHERE QuestId = @QuestId;

SELECT QuestId, SlotIndex, RewardType, ItemId, Amount
FROM world.QuestRewards
WHERE QuestId = @QuestId
ORDER BY SlotIndex;

SELECT QuestId, SpeechKind, LineIndex, Text, Color
FROM world.QuestSpeeches
WHERE QuestId = @QuestId
ORDER BY SpeechKind, LineIndex;
END;

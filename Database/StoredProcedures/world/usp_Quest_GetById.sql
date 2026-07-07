-- Returns 3 result sets in fixed order: Quest, QuestRewards, QuestSpeeches.
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

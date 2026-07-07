CREATE PROCEDURE world.usp_Quest_GetAll
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
    ORDER BY QuestId;
END;

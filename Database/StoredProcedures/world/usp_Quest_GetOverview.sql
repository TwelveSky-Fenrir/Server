CREATE OR ALTER PROCEDURE world.usp_Quest_GetOverview
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT QuestId,
           Subject,
           Sort,
           Level,
           MoneyReward,
           ExperienceReward,
           ItemRewardCount,
           SpeechLineCount
    FROM world.vw_QuestOverview
    ORDER BY QuestId;
END;

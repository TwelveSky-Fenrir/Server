-- Contract: no parameters -> RS0, one row per quest (world.vw_QuestOverview). Tooling/debugging helper only
-- (e.g. "which quests actually pay gold", "which quests have barely any dialogue") -- GameServer's own
-- boot-time cache load uses world.usp_Quest_GetAll / world.usp_QuestReward_GetAll / world.usp_QuestSpeech_GetAll
-- directly, never this view.
-- Ordered by QuestId for a stable, readable listing.
-- Idempotent: yes (read-only). Errors: none.
CREATE PROCEDURE world.usp_Quest_GetOverview
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

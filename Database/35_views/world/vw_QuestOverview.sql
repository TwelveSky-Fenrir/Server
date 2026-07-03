-- Quest-with-child-rows-aggregated shape, meant to be read from inside a retrieval procedure (security model:
-- views are never granted/called directly by app code -- see 60_permissions/001_roles.sql). Tooling/debugging
-- aid (e.g. "which quests actually pay gold", "which quests have barely any dialogue") -- GameServer's own
-- boot-time cache load uses world.usp_Quest_GetAll / world.usp_QuestReward_GetAll / world.usp_QuestSpeech_GetAll
-- directly, never this view.
-- Reward figures are SUMmed per-quest rather than picked from an arbitrary row: RewardType is not unique per
-- quest (a quest can have both a Money and an Experience reward across its up to 3 slots), so a naive join
-- would either duplicate the base quest row (one per reward slot) or arbitrarily pick one -- aggregating in a
-- derived table first (one row per QuestId) avoids both. ItemRewardCount is a COUNT, not the ItemId itself,
-- for the same reason (a quest could carry an Item reward in more than one slot).
CREATE VIEW world.vw_QuestOverview
AS
SELECT q.QuestId,
       q.Subject,
       q.Sort,
       q.Level,
       ISNULL(reward.MoneyReward, 0)      AS MoneyReward,
       ISNULL(reward.ExperienceReward, 0) AS ExperienceReward,
       ISNULL(reward.ItemRewardCount, 0)  AS ItemRewardCount,
       ISNULL(speech.SpeechLineCount, 0)  AS SpeechLineCount
FROM world.Quests q
         LEFT JOIN (SELECT QuestId,
                           SUM(CASE WHEN RewardType = 2 THEN Amount ELSE 0 END) AS MoneyReward,
                           SUM(CASE WHEN RewardType = 4 THEN Amount ELSE 0 END) AS ExperienceReward,
                           COUNT_BIG(CASE WHEN RewardType = 6 THEN 1 END)       AS ItemRewardCount
                    FROM world.QuestRewards
                    GROUP BY QuestId) reward ON reward.QuestId = q.QuestId
         LEFT JOIN (SELECT QuestId, COUNT_BIG(*) AS SpeechLineCount
                    FROM world.QuestSpeeches
                    GROUP BY QuestId) speech ON speech.QuestId = q.QuestId;

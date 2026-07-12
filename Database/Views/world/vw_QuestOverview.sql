-- Tooling/debugging view. Reward figures are SUMmed per-quest in a derived table before joining, since
-- RewardType is not unique per quest (up to 3 reward slots) and a naive join would duplicate quest rows.
-- Not an indexed-view candidate as written: LEFT OUTER JOIN and derived-table (subquery-in-FROM) expressions
-- are both disallowed in indexed views (Microsoft Learn, "Create indexed views" -- Additional requirements),
-- and this view needs both to show a zero, not a missing row, for a quest with no rewards/speeches.
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

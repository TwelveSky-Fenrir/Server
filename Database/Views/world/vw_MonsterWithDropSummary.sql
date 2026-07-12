-- Loot-table summary per monster for tooling. Child-table counts are pre-aggregated in derived tables
-- before joining so a monster with rows in multiple drop tables isn't double-counted by a naive JOIN.
-- Not an indexed-view candidate as written: LEFT OUTER JOIN and derived-table (subquery-in-FROM) expressions
-- are both disallowed in indexed views (Microsoft Learn, "Create indexed views" -- Additional requirements),
-- and this view needs both to show a zero, not a missing row, for a monster with no rows in a given drop table.
CREATE VIEW world.vw_MonsterWithDropSummary
AS
SELECT m.MonsterId,
       m.Name,
       m.RealLevel,
       m.Life,
       dm.DropRate                                      AS MoneyDropRate,
       dm.MinAmount                                     AS MoneyMinAmount,
       dm.MaxAmount                                     AS MoneyMaxAmount,
       ISNULL(dp.PotionSlotCount, 0)                    AS PotionSlotCount,
       ISNULL(de.ExtraItemSlotCount, 0)                 AS ExtraItemSlotCount,
       ISNULL(dc.CategoryRateSlotCount, 0)              AS CategoryRateSlotCount,
       CASE WHEN dq.MonsterId IS NULL THEN 0 ELSE 1 END AS HasQuestItemDrop
FROM world.Monsters AS m
         LEFT JOIN world.MonsterDropMoney AS dm
                   ON dm.MonsterId = m.MonsterId
         LEFT JOIN (SELECT MonsterId, COUNT_BIG(*) AS PotionSlotCount
                    FROM world.MonsterDropPotions
                    GROUP BY MonsterId) AS dp
                   ON dp.MonsterId = m.MonsterId
         LEFT JOIN (SELECT MonsterId, COUNT_BIG(*) AS ExtraItemSlotCount
                    FROM world.MonsterDropExtraItems
                    GROUP BY MonsterId) AS de
                   ON de.MonsterId = m.MonsterId
         LEFT JOIN (SELECT MonsterId, COUNT_BIG(*) AS CategoryRateSlotCount
                    FROM world.MonsterDropCategoryRates
                    GROUP BY MonsterId) AS dc
                   ON dc.MonsterId = m.MonsterId
         LEFT JOIN world.MonsterDropQuestItems AS dq
                   ON dq.MonsterId = m.MonsterId;

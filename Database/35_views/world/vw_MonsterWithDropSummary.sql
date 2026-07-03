-- Internal readability view: one row per monster with its loot-table richness summarized as counts,
-- for tooling/debugging (e.g. "which monsters actually have a loot table worth tuning") rather than a
-- GameServer hot path. Plain view, not indexed -- world.* is boot-time-only reference data with no
-- per-tick read pressure, so materializing this aggregate would be pure cost with no benefit.
-- Child-table counts are pre-aggregated in derived tables before joining to world.Monsters so a monster
-- with, say, both potion and extra-item rows doesn't get double-counted by a naive multi-table JOIN.
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

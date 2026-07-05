-- Tooling/debugging view. Each child table's counts are pre-aggregated in derived tables before joining
-- to world.Npcs so one child table's row multiplication can't inflate another's count.
CREATE VIEW world.vw_NpcWithShopSummary
AS
SELECT n.NpcId,
       n.Name,
       n.Tribe,
       n.Type,
       ISNULL(speech.SpeechLineCount, 0)    AS SpeechLineCount,
       ISNULL(menu.MenuOptionCount, 0)      AS MenuOptionCount,
       ISNULL(shop.ShopItemCount, 0)        AS ShopItemCount,
       ISNULL(skill.SkillOfferCount, 0)     AS SkillOfferCount,
       ISNULL(gamble.GambleCostRowCount, 0) AS GambleCostRowCount
FROM world.Npcs n
         LEFT JOIN (SELECT NpcId, COUNT_BIG(*) AS SpeechLineCount
                    FROM world.NpcSpeeches
                    GROUP BY NpcId) speech ON speech.NpcId = n.NpcId
         LEFT JOIN (SELECT NpcId, COUNT_BIG(*) AS MenuOptionCount
                    FROM world.NpcMenuOptions
                    GROUP BY NpcId) menu ON menu.NpcId = n.NpcId
         LEFT JOIN (SELECT NpcId, COUNT_BIG(*) AS ShopItemCount
                    FROM world.NpcShopItems
                    WHERE ItemId IS NOT NULL
                    GROUP BY NpcId) shop ON shop.NpcId = n.NpcId
         LEFT JOIN (SELECT NpcId, COUNT_BIG(*) AS SkillOfferCount
                    FROM world.NpcSkillOffers
                    WHERE SkillId IS NOT NULL
                    GROUP BY NpcId) skill ON skill.NpcId = n.NpcId
         LEFT JOIN (SELECT NpcId, COUNT_BIG(*) AS GambleCostRowCount
                    FROM world.NpcGambleCosts
                    GROUP BY NpcId) gamble ON gamble.NpcId = n.NpcId;

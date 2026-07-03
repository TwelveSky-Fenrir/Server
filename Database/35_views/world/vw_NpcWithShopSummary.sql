-- Npc-with-child-row-counts shape, meant to be read from inside a retrieval procedure (security model:
-- views are never granted/called directly by app code -- see 60_permissions/001_roles.sql). Tooling/
-- debugging aid (e.g. "which NPCs actually sell something", "which NPCs teach skills") -- GameServer's
-- own boot-time cache load uses the six per-table usp_Npc*_GetAll procedures directly, never this view.
-- LEFT JOINs throughout (an NPC with zero shop slots, say, must still appear with ShopItemCount = 0, not
-- be dropped) aggregated in derived tables before joining to world.Npcs, rather than one single GROUP BY
-- over a wide multi-table join, so each child table's row multiplication can't inflate another child
-- table's count (an NPC with 25 speech lines and 84 shop slots joined naively would multiply to 2100 rows
-- before aggregation).
-- ShopItemCount/SkillOfferCount only count rows with a non-NULL ItemId/SkillId respectively (this
-- generator never actually emits a NULL row today, but the view stays correct if a future manual edit
-- ever adds one). GambleCostRowCount and MenuOptionCount have no such NULL case (see those tables' own
-- header comments for why: OptionId is NOT NULL, and a gamble cell simply isn't a row when its value is 0).
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

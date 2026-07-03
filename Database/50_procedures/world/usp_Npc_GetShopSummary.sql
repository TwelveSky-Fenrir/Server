-- Contract: no parameters -> RS0 rows { NpcId, Name, Tribe, Type, SpeechLineCount, MenuOptionCount,
--           ShopItemCount, SkillOfferCount, GambleCostRowCount }, one row per NPC, ordered by NpcId.
-- Tooling/debugging helper only (e.g. "which NPCs actually sell something") -- reads
-- world.vw_NpcWithShopSummary internally (security model: views are an internal T-SQL readability tool a
-- procedure's body may use -- see 60_permissions/001_roles.sql). GameServer's own boot-time cache load
-- uses world.usp_Npc_GetAll plus the five per-child-table usp_Npc*_GetAll procedures directly, never this one.
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_Npc_GetShopSummary
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT NpcId,
       Name,
       Tribe,
       Type,
       SpeechLineCount,
       MenuOptionCount,
       ShopItemCount,
       SkillOfferCount,
       GambleCostRowCount
FROM world.vw_NpcWithShopSummary
ORDER BY NpcId;
END;

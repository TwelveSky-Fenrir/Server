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

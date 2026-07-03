-- Contract: @NpcId INT -> six result sets for a single NPC, in this order: RS0 zero or one row (the
--           world.Npcs row itself); RS1 its world.NpcSpeeches rows; RS2 its world.NpcMenuOptions rows;
--           RS3 its world.NpcShopItems rows; RS4 its world.NpcSkillOffers rows; RS5 its
--           world.NpcGambleCosts rows.
-- Tooling/debugging helper only -- GameServer itself always loads the full set via world.usp_Npc_GetAll
-- plus the five per-child-table usp_Npc*_GetAll procedures once at boot, never a single NPC (with its
-- children) per request. Bundled as one multi-result-set call rather than six separate procedures purely
-- because "show me everything about this one NPC" is the actual tooling use case.
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_Npc_GetById @NpcId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT NpcId,
       Name,
       Tribe,
       Type,
       DataSortNumber2D,
       DataSortNumber3D,
       Size1,
       Size2,
       Size3
FROM world.Npcs
WHERE NpcId = @NpcId;

SELECT NpcId, SpeechGroup, SpeechIndex, Text
FROM world.NpcSpeeches
WHERE NpcId = @NpcId
ORDER BY SpeechGroup, SpeechIndex;

SELECT NpcId, SlotIndex, OptionId
FROM world.NpcMenuOptions
WHERE NpcId = @NpcId
ORDER BY SlotIndex;

SELECT NpcId, ShopPage, SlotIndex, ItemId
FROM world.NpcShopItems
WHERE NpcId = @NpcId
ORDER BY ShopPage, SlotIndex;

SELECT NpcSkillOfferId,
       NpcId,
       ArrayKind,
       Tier,
       Dim2,
       Dim3,
       SlotIndex,
       SkillId
FROM world.NpcSkillOffers
WHERE NpcId = @NpcId
ORDER BY ArrayKind, Tier, Dim2, Dim3, SlotIndex;

SELECT NpcId, GambleTier, CostIndex, Value
FROM world.NpcGambleCosts
WHERE NpcId = @NpcId
ORDER BY GambleTier, CostIndex;
END;

-- Contract: no parameters -> RS0 rows { NpcSkillOfferId INT, NpcId INT, ArrayKind TINYINT, Tier TINYINT,
--           Dim2 TINYINT NULL, Dim3 TINYINT NULL, SlotIndex TINYINT, SkillId INT NULL }, one row per
--           populated skill-offer slot from either legacy array (234 rows in this build -- see
--           world.NpcSkillOffers header for the ArrayKind/Tier/Dim2/Dim3 mapping), ordered so all of one
--           NPC's offers are contiguous.
-- Bulk fetch, not per-NPC: called once at GameServer boot alongside world.usp_Npc_GetAll to populate the
-- in-memory NPC cache (world.* is read-mostly reference data, never queried per-tick).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_NpcSkillOffer_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT NpcSkillOfferId,
       NpcId,
       ArrayKind,
       Tier,
       Dim2,
       Dim3,
       SlotIndex,
       SkillId
FROM world.NpcSkillOffers
ORDER BY NpcId, ArrayKind, Tier, Dim2, Dim3, SlotIndex;
END;

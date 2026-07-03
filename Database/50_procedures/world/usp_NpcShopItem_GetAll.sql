-- Contract: no parameters -> RS0 rows { NpcId INT, ShopPage TINYINT, SlotIndex TINYINT, ItemId INT NULL },
--           one row per populated shop slot (467 rows in this build), ordered so all of one NPC's pages
--           and slots are contiguous.
-- Bulk fetch, not per-NPC: called once at GameServer boot alongside world.usp_Npc_GetAll to populate the
-- in-memory NPC cache (world.* is read-mostly reference data, never queried per-tick).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_NpcShopItem_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT NpcId, ShopPage, SlotIndex, ItemId
FROM world.NpcShopItems
ORDER BY NpcId, ShopPage, SlotIndex;
END;

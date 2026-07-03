-- Contract: no parameters -> RS0 rows { NpcId INT, Name NVARCHAR(28), Tribe TINYINT, Type TINYINT,
--           DataSortNumber2D INT, DataSortNumber3D INT, Size1 INT, Size2 INT, Size3 INT }, every real NPC
--           (131 rows in this build), ordered by NpcId.
-- Called once at GameServer boot to populate the in-memory NPC cache, alongside the five
-- usp_Npc*_GetAll child-table procedures -- world.* is read-mostly reference data, never queried
-- per-tick (see 60_permissions/001_roles.sql).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_Npc_GetAll
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
ORDER BY NpcId;
END;

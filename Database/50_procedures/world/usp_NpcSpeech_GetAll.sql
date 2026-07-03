-- Contract: no parameters -> RS0 rows { NpcId INT, SpeechGroup TINYINT, SpeechIndex TINYINT,
--           Text NVARCHAR(51) }, one row per populated speech line (1720 rows in this build), ordered so
--           all of one NPC's lines are contiguous and in display order.
-- Bulk fetch, not per-NPC: called once at GameServer boot alongside world.usp_Npc_GetAll to populate the
-- in-memory NPC cache (world.* is read-mostly reference data, never queried per-tick).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_NpcSpeech_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT NpcId, SpeechGroup, SpeechIndex, Text
FROM world.NpcSpeeches
ORDER BY NpcId, SpeechGroup, SpeechIndex;
END;

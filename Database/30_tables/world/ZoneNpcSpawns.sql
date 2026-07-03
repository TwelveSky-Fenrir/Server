-- Normalized from ZONENPCINFODATA.NpcNumber/NpcCoord/NpcAngle (002.BIN, Header/Protocol/STRUCT.h:1380-
-- 1386): the legacy struct is a fixed 100-slot-per-zone array where only the first TotalNpcNum slots are
-- ever populated. Per "normalize, don't transliterate", this table stores one row per POPULATED slot
-- only (117 live zones * up to 100 slots collapses to 291 real rows -- see
-- 70_seed/world/021_zone_npc_spawns.sql), not 100 columns or 100 rows-per-zone regardless of use.
-- NpcId is NULL rather than 0 for a slot whose legacy NpcNumber entry is 0 (no NPC placed) -- 0 is not
-- a valid world.Npcs row (cross-domain FK convention; world.Npcs is designed in a parallel domain).
-- SlotIndex keeps the original 0-based array position for traceability/debugging even though it carries
-- no gameplay meaning on its own.
CREATE TABLE world.ZoneNpcSpawns
(
    ZoneNumber SMALLINT NOT NULL,
    SlotIndex  SMALLINT NOT NULL,
    NpcId      INT NULL,
    PosX       REAL     NOT NULL,
    PosY       REAL     NOT NULL,
    PosZ       REAL     NOT NULL,
    Angle      REAL     NOT NULL,
    CONSTRAINT PK_ZoneNpcSpawns PRIMARY KEY CLUSTERED (ZoneNumber, SlotIndex),
    CONSTRAINT CK_ZoneNpcSpawns_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 99),
    CONSTRAINT FK_ZoneNpcSpawns_Zones FOREIGN KEY (ZoneNumber) REFERENCES world.Zones (ZoneNumber),
    CONSTRAINT FK_ZoneNpcSpawns_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId)
);

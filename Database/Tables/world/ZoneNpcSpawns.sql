-- Normalized from ZONENPCINFODATA.NpcNumber/NpcCoord/NpcAngle; one row per populated slot only (of a 100-slot-per-zone array).
-- NpcId is NULL (never 0) when the legacy slot has no NPC placed.
CREATE TABLE world.ZoneNpcSpawns
(
    ZoneNumber SMALLINT NOT NULL,
    SlotIndex  SMALLINT NOT NULL,
    NpcId      INT      NULL,
    PosX       REAL     NOT NULL,
    PosY       REAL     NOT NULL,
    PosZ       REAL     NOT NULL,
    Angle      REAL     NOT NULL,
    CONSTRAINT PK_ZoneNpcSpawns PRIMARY KEY CLUSTERED (ZoneNumber, SlotIndex),
    CONSTRAINT CK_ZoneNpcSpawns_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 99),
    CONSTRAINT FK_ZoneNpcSpawns_Zones FOREIGN KEY (ZoneNumber) REFERENCES world.Zones (ZoneNumber),
    CONSTRAINT FK_ZoneNpcSpawns_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId)
);

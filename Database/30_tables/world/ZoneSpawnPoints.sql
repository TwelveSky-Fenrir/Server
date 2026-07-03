-- Normalized from ZONEMOVEDATA.StartCoord/StartCoordZone (003.BIN, Header/Protocol/STRUCT.h:1369-1378):
-- only the first StartCoordNum of each zone's 100-slot arrays are populated (117 live zones collapse to
-- 413 real rows out of a 11700-slot ceiling -- see 70_seed/world/023_zone_spawn_points.sql).
-- ZoneNumber is the zone you LAND in; FromZoneNumber is which zone you arrived FROM (StartCoordZone).
-- FromZoneNumber is NULL, never 0, for the same two legacy-data reasons as world.ZonePortals.TargetZoneNumber,
-- but the split skews the other way here: of 413 populated slots, ~41% name a zone number this build
-- has no DATA/WORLD/Z0NN.WM for (a source zone that exists in the legacy data but was never shipped in
-- this build) and a further ~7% are a literal StartCoordZone of 0 (no recorded source). Both fold to
-- NULL (cross-domain FK convention: 0/dangling -> NULL).
CREATE TABLE world.ZoneSpawnPoints
(
    ZoneNumber     SMALLINT NOT NULL,
    SlotIndex      SMALLINT NOT NULL,
    FromZoneNumber SMALLINT NULL,
    PosX           REAL     NOT NULL,
    PosY           REAL     NOT NULL,
    PosZ           REAL     NOT NULL,
    CONSTRAINT PK_ZoneSpawnPoints PRIMARY KEY CLUSTERED (ZoneNumber, SlotIndex),
    CONSTRAINT CK_ZoneSpawnPoints_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 99),
    CONSTRAINT FK_ZoneSpawnPoints_Zones FOREIGN KEY (ZoneNumber) REFERENCES world.Zones (ZoneNumber),
    CONSTRAINT FK_ZoneSpawnPoints_FromZone FOREIGN KEY (FromZoneNumber) REFERENCES world.Zones (ZoneNumber)
);

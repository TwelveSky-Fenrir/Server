-- Normalized from ZONEMOVEDATA.Xyz/NextZone (003.BIN, Header/Protocol/STRUCT.h:1369-1378): only the
-- first NextZoneNum of each zone's 100-slot arrays are populated (117 live zones collapse to 413 real
-- rows out of a 11700-slot ceiling -- see 70_seed/world/022_zone_portals.sql).
-- TargetZoneNumber is NULL, never 0, for two distinct legacy-data reasons observed while generating the
-- real seed data (413 populated slots total): (a) the raw NextZone value is literally 0 for ~54% of
-- them (a populated "trigger volume" with no travel destination -- e.g. a non-portal trigger reusing
-- the same array slot shape), and (b) a further ~2% name a zone number this build has no
-- DATA/WORLD/Z0NN.WM for (a destination that exists in the legacy data but was never shipped in this
-- build). Both cases are indistinguishable from "no reference" at the FK level, so both fold to NULL
-- (cross-domain FK convention: 0/dangling -> NULL, never a literal 0 stored against a NOT-NULL-enforced
-- PK domain).
CREATE TABLE world.ZonePortals
(
    ZoneNumber       SMALLINT NOT NULL,
    SlotIndex        SMALLINT NOT NULL,
    TriggerX         REAL     NOT NULL,
    TriggerY         REAL     NOT NULL,
    TriggerZ         REAL     NOT NULL,
    TargetZoneNumber SMALLINT NULL,
    CONSTRAINT PK_ZonePortals PRIMARY KEY CLUSTERED (ZoneNumber, SlotIndex),
    CONSTRAINT CK_ZonePortals_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 99),
    CONSTRAINT FK_ZonePortals_Zones FOREIGN KEY (ZoneNumber) REFERENCES world.Zones (ZoneNumber),
    CONSTRAINT FK_ZonePortals_TargetZone FOREIGN KEY (TargetZoneNumber) REFERENCES world.Zones (ZoneNumber)
);

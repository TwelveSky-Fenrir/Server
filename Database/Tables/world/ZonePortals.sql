-- Normalized from ZONEMOVEDATA.Xyz/NextZone: only the first NextZoneNum of each zone's 100-slot arrays are populated.
-- TargetZoneNumber is NULL (never 0) both when the raw NextZone is literally 0 (no destination) and when it names a zone this build never shipped.
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

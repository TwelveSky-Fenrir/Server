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

CREATE TABLE world.Zones
(
    ZoneNumber    SMALLINT NOT NULL,
    DefaultSpawnX REAL     NOT NULL,
    DefaultSpawnY REAL     NOT NULL,
    DefaultSpawnZ REAL     NOT NULL,
    CONSTRAINT PK_Zones PRIMARY KEY CLUSTERED (ZoneNumber)
);

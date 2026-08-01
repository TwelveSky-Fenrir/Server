-- One row per zone this build actually ships (117 of the legacy 350-slot array); ZoneNumber is the legacy 1-based zone number, never re-assigned.
-- DefaultSpawnX/Y/Z is ZONEMOVEDATA.mFirstCoord, the fallback spawn point when no more specific world.ZoneSpawnPoints row applies.
CREATE TABLE world.Zones
(
    ZoneNumber    SMALLINT NOT NULL,
    DefaultSpawnX REAL     NOT NULL,
    DefaultSpawnY REAL     NOT NULL,
    DefaultSpawnZ REAL     NOT NULL,
    CONSTRAINT PK_Zones PRIMARY KEY CLUSTERED (ZoneNumber)
);

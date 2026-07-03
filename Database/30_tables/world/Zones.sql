-- One row per zone number this build actually ships, out of the legacy 350-slot MAX_ZONE_NUMBER_NUM
-- array shared by 002.BIN (ZONENPCINFODATA) and 003.BIN (ZONEMOVEDATA) -- "ships" meaning it has a
-- matching DATA/WORLD/Z0NN.WM world-mesh file in this build (117 of 350 slots do; confirmed by direct
-- file enumeration, see Fenrir.Tools.LegacyDataImport ZoneSeedGenerator). A row here means "a player can
-- actually be in this zone", not "this legacy array slot exists" -- the other ~233 slots are dead/
-- never-shipped zone numbers and are deliberately not stored.
-- ZoneNumber is the legacy 1-based zone number (record's 0-based array index + 1 in both .BIN files),
-- never re-assigned -- part of the cross-domain PK contract other domains' FKs rely on.
-- DefaultSpawnX/Y/Z is ZONEMOVEDATA.mFirstCoord, the zone's fallback spawn point when no more specific
-- world.ZoneSpawnPoints row applies to the zone a character is arriving from.
CREATE TABLE world.Zones
(
    ZoneNumber    SMALLINT NOT NULL,
    DefaultSpawnX REAL     NOT NULL,
    DefaultSpawnY REAL     NOT NULL,
    DefaultSpawnZ REAL     NOT NULL,
    CONSTRAINT PK_Zones PRIMARY KEY CLUSTERED (ZoneNumber)
);

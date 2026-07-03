-- Already one row per source CSV row (WORLD_REGION_INFO, Header/Protocol/STRUCT.h:1327-1335, parsed
-- from DATA/SUMMON/*.WREGION.csv) -- no further normalization needed, unlike the other 4 tables in this
-- batch. Surrogate MonsterSpawnRegionId IDENTITY PK: there is no natural key, many rows legitimately
-- share the same ZoneNumber+MonsterId (one row per summon-group definition, not one row per monster/zone
-- pair).
-- MonsterId is NULL, never 0, for a row whose legacy mIndex is 0 -- 0 is not a valid world.Monsters row
-- (cross-domain FK convention; world.Monsters is designed in a parallel domain). Not actually observed
-- in this build's data (every row has a real MonsterId), but kept nullable defensively.
-- ZoneNumber DEVIATES from a plain NOT NULL FK, discovered against real data while generating the seed:
-- of the 132 distinct zone numbers embedded in *.WREGION.csv file names, 91 (~69%) have no matching
-- DATA/WORLD/Z0NN.WM in this build -- i.e. ~49% of all 21960 rows name a zone this build never shipped
-- (dead/cut summon definitions left behind in the data, the same "build doesn't actually have this zone"
-- phenomenon world.ZonePortals/world.ZoneSpawnPoints hit, just far more pronounced here since spawn
-- regions come from an independent SUMMON/ data source with no dependency on a zone actually having a
-- compiled mesh). A NOT NULL FK would reject roughly half the table, so ZoneNumber is NULLable and
-- dead-zone references are translated to NULL rather than dropping that data outright; SourceFileName
-- (always embeds the raw "Z0NN" prefix) is the lossless fallback for recovering the original zone number
-- if ever needed.
-- SourceFileName is kept verbatim (e.g. "Z040_SUMMONBOSSMONSTER.WREGION.csv") rather than parsed into a
-- kind/variant column -- a later consumer can pattern-match "BOSS"/"_<n>" suffixes if it needs that
-- distinction; not worth over-engineering here.
CREATE TABLE world.MonsterSpawnRegions
(
    MonsterSpawnRegionId INT IDENTITY (1,1) NOT NULL,
    ZoneNumber           SMALLINT NULL,
    SourceFileName       NVARCHAR(100) NOT NULL,
    Value01              INT NOT NULL, -- legacy mVALUE01, purpose not yet documented upstream
    MonsterId            INT NULL,
    Value03              INT NOT NULL, -- legacy mVALUE03, purpose not yet documented upstream
    Number               INT NOT NULL, -- legacy mNumber: how many monsters to summon at this region
    LocationX            INT NOT NULL,
    LocationY            INT NOT NULL,
    LocationZ            INT NOT NULL,
    Radius               INT NOT NULL,
    CONSTRAINT PK_MonsterSpawnRegions PRIMARY KEY CLUSTERED (MonsterSpawnRegionId),
    CONSTRAINT FK_MonsterSpawnRegions_Zones FOREIGN KEY (ZoneNumber) REFERENCES world.Zones (ZoneNumber),
    CONSTRAINT FK_MonsterSpawnRegions_Monsters FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId)
);

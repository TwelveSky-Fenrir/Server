-- One row per source WREGION.csv row (WORLD_REGION_INFO); surrogate IDENTITY PK since multiple rows legitimately share the same Zone+Monster.
-- ZoneNumber is nullable: ~49% of rows name a zone this build never shipped; SourceFileName keeps the raw "Z0NN" prefix as a lossless fallback.
CREATE TABLE world.MonsterSpawnRegions
(
    MonsterSpawnRegionId INT IDENTITY (1,1) NOT NULL,
    ZoneNumber           SMALLINT           NULL,
    SourceFileName       NVARCHAR(100)      NOT NULL,
    Value01              INT                NOT NULL, -- legacy mVALUE01, purpose not yet documented upstream
    MonsterId            INT                NULL,
    Value03              INT                NOT NULL, -- legacy mVALUE03, purpose not yet documented upstream
    Number               INT                NOT NULL, -- legacy mNumber: how many monsters to summon at this region
    LocationX            INT                NOT NULL,
    LocationY            INT                NOT NULL,
    LocationZ            INT                NOT NULL,
    Radius               INT                NOT NULL,
    CONSTRAINT PK_MonsterSpawnRegions PRIMARY KEY CLUSTERED (MonsterSpawnRegionId),
    CONSTRAINT FK_MonsterSpawnRegions_Zones FOREIGN KEY (ZoneNumber) REFERENCES world.Zones (ZoneNumber),
    CONSTRAINT FK_MonsterSpawnRegions_Monsters FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId)
);

CREATE TABLE world.MonsterSpawnRegions
(
    MonsterSpawnRegionId INT IDENTITY (1,1) NOT NULL,
    ZoneNumber           SMALLINT           NULL,
    SourceFileName       NVARCHAR(100)      NOT NULL,
    Value01              INT                NOT NULL, 
    MonsterId            INT                NULL,
    Value03              INT                NOT NULL, 
    Number               INT                NOT NULL, 
    LocationX            INT                NOT NULL,
    LocationY            INT                NOT NULL,
    LocationZ            INT                NOT NULL,
    Radius               INT                NOT NULL,
    CONSTRAINT PK_MonsterSpawnRegions PRIMARY KEY CLUSTERED (MonsterSpawnRegionId),
    CONSTRAINT FK_MonsterSpawnRegions_Zones FOREIGN KEY (ZoneNumber) REFERENCES world.Zones (ZoneNumber),
    CONSTRAINT FK_MonsterSpawnRegions_Monsters FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId)
);

CREATE TABLE game.MonsterBossRespawnTimers
(
    MonsterSpawnRegionId INT          NOT NULL,
    NextSpawnUtc         DATETIME2(3) NOT NULL,
    CONSTRAINT PK_MonsterBossRespawnTimers PRIMARY KEY CLUSTERED (MonsterSpawnRegionId),
    CONSTRAINT FK_MonsterBossRespawnTimers_World_Region FOREIGN KEY (MonsterSpawnRegionId)
        REFERENCES world.MonsterSpawnRegions (MonsterSpawnRegionId)
);

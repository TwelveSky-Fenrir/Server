-- Persisted "next spawn" deadline for the handful of named-boss MonsterSpawnRegions slots whose respawn
-- cooldown must survive a GameServer restart (legacy LNW33 YangGok persistence: S10_MySummon.cpp's
-- YG_SaveNormalBossNext/YG_LoadNormalBossNext, one flat file per (zone, slot, bossId) under
-- YanggokNormalBossTimer\ -- normalized here into one durable row per region instead).
-- A row only exists between "this boss just died" and "this boss successfully respawned" -- MonsterSpawnScheduler
-- deletes it once the slot pops again, so an empty table is the common case, not a bootstrap artifact to seed.
CREATE TABLE game.MonsterBossRespawnTimers
(
    MonsterSpawnRegionId INT NOT NULL,
    NextSpawnUtc         DATETIME2(3) NOT NULL,
    CONSTRAINT PK_MonsterBossRespawnTimers PRIMARY KEY CLUSTERED (MonsterSpawnRegionId),
    CONSTRAINT FK_MonsterBossRespawnTimers_Region FOREIGN KEY (MonsterSpawnRegionId)
        REFERENCES world.MonsterSpawnRegions (MonsterSpawnRegionId)
);

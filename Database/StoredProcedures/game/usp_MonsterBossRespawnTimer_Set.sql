-- database/50_procedures/game/usp_MonsterBossRespawnTimer_Set.sql
-- Idempotent upsert via DELETE-then-INSERT (never MERGE, matching usp_WorldStateAllianceOffer_Set).
-- Called once per boss death (arming the persisted deadline) and once per successful respawn (clearing it --
-- callers pass @NextSpawnUtc = the current time, which MonsterSpawnScheduler's own boot-load treats as "due now").
CREATE PROCEDURE game.usp_MonsterBossRespawnTimer_Set @MonsterSpawnRegionId INT,
                                                      @NextSpawnUtc DATETIME2(3)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DELETE
    FROM game.MonsterBossRespawnTimers
    WHERE MonsterSpawnRegionId = @MonsterSpawnRegionId;

    INSERT INTO game.MonsterBossRespawnTimers (MonsterSpawnRegionId, NextSpawnUtc)
    VALUES (@MonsterSpawnRegionId, @NextSpawnUtc);
END;

-- database/50_procedures/game/usp_MonsterBossRespawnTimer_GetAll.sql
-- Contract: no parameters -> RS0 { MonsterSpawnRegionId, NextSpawnUtc }, one row per boss slot with a
-- still-pending (or just-recorded) respawn deadline. Boot-load only -- MonsterSpawnScheduler never re-queries
-- this mid-tick (SQL is a durability journal, never read again once loaded).
-- Read-only, safe to retry.
CREATE PROCEDURE game.usp_MonsterBossRespawnTimer_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT MonsterSpawnRegionId, NextSpawnUtc
    FROM game.MonsterBossRespawnTimers;
END;

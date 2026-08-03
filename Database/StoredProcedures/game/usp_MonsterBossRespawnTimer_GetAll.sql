CREATE PROCEDURE game.usp_MonsterBossRespawnTimer_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT MonsterSpawnRegionId, NextSpawnUtc
    FROM game.MonsterBossRespawnTimers;
END;

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

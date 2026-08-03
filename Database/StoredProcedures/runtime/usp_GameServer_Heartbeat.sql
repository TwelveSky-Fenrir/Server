CREATE PROCEDURE runtime.usp_GameServer_Heartbeat @ShardId TINYINT,
                                                  @Host NVARCHAR(64),
                                                  @Port INT,
                                                  @Ccu INT,
                                                  @Capacity INT,
                                                  @TickP99Ms REAL
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    UPDATE runtime.GameServerDirectory
    SET Host             = @Host,
        Port             = @Port,
        Ccu              = @Ccu,
        Capacity         = @Capacity,
        TickP99Ms        = @TickP99Ms,
        LastHeartbeatUtc = SYSUTCDATETIME()
    WHERE ShardId = @ShardId;

    IF
        @@ROWCOUNT = 0
        INSERT INTO runtime.GameServerDirectory
            (ShardId, Host, Port, Ccu, Capacity, TickP99Ms, LastHeartbeatUtc)
        VALUES (@ShardId, @Host, @Port, @Ccu, @Capacity, @TickP99Ms, SYSUTCDATETIME());
END;

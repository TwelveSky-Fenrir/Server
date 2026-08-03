CREATE PROCEDURE runtime.usp_GameServer_GetDirectory @StalenessCutoffSeconds INT = 15
    WITH NATIVE_COMPILATION ,
        SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    SELECT ShardId, Host, Port, Ccu, Capacity, TickP99Ms
    FROM runtime.GameServerDirectory
    WHERE LastHeartbeatUtc > DATEADD(SECOND, -@StalenessCutoffSeconds, SYSUTCDATETIME());
END;

CREATE PROCEDURE runtime.usp_GameServer_MarkUnreachable @ShardId TINYINT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.GameServerDirectory
    WHERE ShardId = @ShardId;
END;

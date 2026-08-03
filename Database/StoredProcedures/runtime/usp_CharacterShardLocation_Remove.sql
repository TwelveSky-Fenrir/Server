CREATE PROCEDURE runtime.usp_CharacterShardLocation_Remove @CharacterId INT,
                                                           @ShardId TINYINT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.CharacterShardLocation
    WHERE CharacterId = @CharacterId
      AND ShardId = @ShardId;
END;

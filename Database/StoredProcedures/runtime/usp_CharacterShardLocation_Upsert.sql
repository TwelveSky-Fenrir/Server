-- Called once per world-entry (EnterWorldService), never per tick or per movement. UPDATE-then-INSERT
-- upsert (no MERGE, per architecture reference), same shape as usp_GameServer_Heartbeat.
CREATE PROCEDURE runtime.usp_CharacterShardLocation_Upsert @CharacterId INT,
                                                           @ShardId TINYINT,
                                                           @MapId SMALLINT,
                                                           @AvatarName NVARCHAR(13),
                                                           @Tribe TINYINT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    UPDATE runtime.CharacterShardLocation
    SET ShardId     = @ShardId,
        MapId       = @MapId,
        AvatarName  = @AvatarName,
        Tribe       = @Tribe,
        LastSeenUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    IF
        @@ROWCOUNT = 0
        INSERT INTO runtime.CharacterShardLocation
            (CharacterId, ShardId, MapId, AvatarName, Tribe, LastSeenUtc)
        VALUES (@CharacterId, @ShardId, @MapId, @AvatarName, @Tribe, SYSUTCDATETIME());
END;

CREATE PROCEDURE runtime.usp_CharacterShardLocation_FindByName @AvatarName NVARCHAR(13)
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    SELECT l.CharacterId, l.ShardId, l.MapId, l.AvatarName, l.Tribe, l.LastSeenUtc
    FROM runtime.CharacterShardLocation AS l
             INNER JOIN runtime.GameServerDirectory AS d ON d.ShardId = l.ShardId
    WHERE l.AvatarName = @AvatarName
      AND d.LastHeartbeatUtc > DATEADD(SECOND, -15, SYSUTCDATETIME());
END;

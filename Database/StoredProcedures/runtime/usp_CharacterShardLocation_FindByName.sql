-- Rows whose owning shard has gone stale (no heartbeat within the last 15s -- the same freshness window
-- usp_GameServer_GetDirectory already applies) are excluded: a crashed shard's stale rows need no separate
-- cleanup job. Used by WhisperService/FindGuildMemberService as the cross-shard fallback once the local
-- ZoneRegistry lookup misses.
CREATE PROCEDURE runtime.usp_CharacterShardLocation_FindByName @AvatarName NVARCHAR(13)
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
SELECT l.CharacterId, l.ShardId, l.MapId, l.AvatarName, l.Tribe, l.LastSeenUtc
FROM runtime.CharacterShardLocation AS l
         INNER JOIN runtime.GameServerDirectory AS d ON d.ShardId = l.ShardId
WHERE l.AvatarName = @AvatarName
  AND d.LastHeartbeatUtc > DATEADD(SECOND, -15, SYSUTCDATETIME());
END;

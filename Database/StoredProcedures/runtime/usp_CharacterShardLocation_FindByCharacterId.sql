-- Same staleness filter as usp_CharacterShardLocation_FindByName -- see that proc's own remarks. Used by
-- FriendService.LocateAsync as the cross-shard fallback once the local ZoneRegistry lookup misses; Tribe is
-- read back denormalized so the same-tribe gate can be re-applied without a second round trip.
CREATE PROCEDURE runtime.usp_CharacterShardLocation_FindByCharacterId @CharacterId INT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    SELECT l.CharacterId, l.ShardId, l.MapId, l.AvatarName, l.Tribe, l.LastSeenUtc
    FROM runtime.CharacterShardLocation AS l
             INNER JOIN runtime.GameServerDirectory AS d ON d.ShardId = l.ShardId
    WHERE l.CharacterId = @CharacterId
      AND d.LastHeartbeatUtc > DATEADD(SECOND, -15, SYSUTCDATETIME());
END;

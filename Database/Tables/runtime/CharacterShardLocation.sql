-- Cross-shard character-location directory: a same-shard-miss fallback for whisper/friend-locate/guild-find
-- lookups (WhisperService/FriendService/FindGuildMemberService), kept warm by EnterWorldService (upsert,
-- once per world-entry) and Zone's true-disconnect path (remove, fire-and-forget off the tick thread) --
-- never a per-tick write. Same in-memory-OLTP/native-proc shape as runtime.GameServerDirectory: ephemeral,
-- high-churn, session-lifecycle-scoped runtime data, not durable game state.
-- AvatarName width matches game.Characters.Name (NVARCHAR(13), MAX_AVATAR_NAME_LENGTH) and the wire's own
-- FixedString(13) for this field (see e.g. CZ_SECRET_CHAT_SEND's AvatarName).
-- Tribe is denormalized from character.Tribe (already in scope at the write site) purely so
-- FriendService.LocateAsync's existing same-tribe gate can be re-applied on a cross-shard hit without a
-- second round trip.
-- Staleness is read, not written, here: the lookup procs INNER JOIN runtime.GameServerDirectory and require
-- a heartbeat fresher than 15s -- the same freshness window usp_GameServer_GetDirectory already applies --
-- so a crashed shard's rows are auto-filtered with no separate cleanup job.
CREATE TABLE runtime.CharacterShardLocation
(
    CharacterId INT          NOT NULL,
    ShardId     TINYINT      NOT NULL,
    MapId       SMALLINT     NOT NULL,
    AvatarName  NVARCHAR(13) NOT NULL,
    Tribe       TINYINT      NOT NULL,
    LastSeenUtc DATETIME2(3) NOT NULL,
    CONSTRAINT PK_CharacterShardLocation PRIMARY KEY NONCLUSTERED HASH (CharacterId)
        WITH (BUCKET_COUNT = 1024),
    INDEX IX_CharacterShardLocation_AvatarName NONCLUSTERED HASH (AvatarName) WITH (BUCKET_COUNT = 1024)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);

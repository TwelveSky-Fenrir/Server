-- Scoped to ShardId too, not just CharacterId: a stale remove from a shard the character already
-- reconnected away from (e.g. a delayed disconnect cleanup racing a fast reconnect to a different shard)
-- must never clobber the fresh row the new shard's own EnterWorldService upsert just wrote.
CREATE PROCEDURE runtime.usp_CharacterShardLocation_Remove @CharacterId INT,
    @ShardId     TINYINT
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE FROM runtime.CharacterShardLocation
    WHERE CharacterId = @CharacterId
      AND ShardId = @ShardId;
END;

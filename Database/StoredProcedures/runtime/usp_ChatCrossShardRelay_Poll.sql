-- Batched per-shard poll, called once per ChatCrossShardRelayHost cycle: returns every whisper row addressed
-- to @ShardId (TargetShardId = @ShardId) since this shard's own last poll, advances this shard's own cursor
-- past everything just returned, and reaps rows older than @RetentionSeconds regardless of target shard (see
-- ChatCrossShardRelay's own remarks for why reap is time-based, not cursor-based -- a dead shard's cursor must
-- never pin the table's memory-optimized storage forever).
--
-- Point-to-point, not fan-out: filters on TargetShardId = @ShardId (this shard's own held cursor is expressed
-- against that same filtered subset of RelayId values), byte-for-byte the same shape as its sibling
-- usp_SocialCrossShardRelay_Poll -- see that proc's own header for why advancing the cursor to MAX(RelayId) of
-- the TargetShardId-filtered set (not the table's global MAX) is still correct despite RelayId being one
-- IDENTITY sequence shared by every shard's rows.
--
-- Interpreted, not native-compiled, for the same reason usp_SocialCrossShardRelay_Poll's own header gives:
-- this one combines a scalar cursor read, an aggregate MAX, a row-returning SELECT, an UPDATE-or-INSERT cursor
-- upsert, and a time-based DELETE in one call -- past the "one simple statement type" subset this codebase's
-- natively-compiled procs stick to. WITH (SNAPSHOT) table hints make the required isolation level explicit
-- regardless of the caller's ambient transaction/autocommit state.
CREATE PROCEDURE runtime.usp_ChatCrossShardRelay_Poll @ShardId TINYINT,
                                                      @RetentionSeconds INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE @LastRelayId BIGINT = 0;
    DECLARE @NewLastRelayId BIGINT = NULL;

    SELECT @LastRelayId = LastRelayId
    FROM runtime.ChatCrossShardRelayCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT @NewLastRelayId = MAX(RelayId)
    FROM runtime.ChatCrossShardRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND TargetShardId = @ShardId;

    SELECT RelayId,
           SourceShardId,
           SourceCharacterId,
           SourceAvatarName,
           TargetShardId,
           TargetCharacterId,
           TargetAvatarName,
           Content,
           SenderAuthType
    FROM runtime.ChatCrossShardRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND TargetShardId = @ShardId
    ORDER BY RelayId ASC;

    IF @NewLastRelayId IS NOT NULL
        BEGIN
            UPDATE runtime.ChatCrossShardRelayCursor WITH (SNAPSHOT)
            SET LastRelayId = @NewLastRelayId
            WHERE ShardId = @ShardId;

            IF @@ROWCOUNT = 0
                INSERT INTO runtime.ChatCrossShardRelayCursor (ShardId, LastRelayId)
                VALUES (@ShardId, @NewLastRelayId);
        END;

    DELETE
    FROM runtime.ChatCrossShardRelay WITH (SNAPSHOT)
    WHERE CreatedAtUtc <= DATEADD(SECOND, -@RetentionSeconds, SYSUTCDATETIME());
END;

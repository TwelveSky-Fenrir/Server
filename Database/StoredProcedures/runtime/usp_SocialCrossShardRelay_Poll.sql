-- Batched per-shard poll, called once per SocialCrossShardRelayHost cycle: returns every row addressed to
-- @ShardId (TargetShardId = @ShardId, both Ask and Answer rows) since this shard's own last poll, advances
-- this shard's own cursor past everything just returned, and reaps rows older than @RetentionSeconds
-- regardless of target shard (see SocialCrossShardRelay's own remarks for why reap is time-based, not
-- cursor-based -- a dead shard's cursor must never pin the table's memory-optimized storage forever).
--
-- Point-to-point, not fan-out: filters on TargetShardId = @ShardId (this shard's own held cursor is
-- expressed against that same filtered subset of RelayId values), unlike the fan-out sibling
-- usp_GuildTribeBroadcastRelay_Poll's SourceShardId <> @ShardId predicate -- see SocialCrossShardRelay's own
-- header for why a social-negotiation message always has exactly one intended recipient shard, never "every
-- other shard". Advancing the cursor to MAX(RelayId) of the TargetShardId-filtered set (not the table's
-- global MAX) is still correct despite RelayId being one IDENTITY sequence shared by every shard's rows,
-- because every future poll re-applies the same TargetShardId = @ShardId filter alongside RelayId > cursor
-- -- rows addressed to other shards never advance or are skipped by this shard's own cursor.
--
-- Interpreted, not native-compiled, for the same reason usp_GuildTribeBroadcastRelay_Poll's own header
-- gives: this one combines a scalar cursor read, an aggregate MAX, a row-returning SELECT, an UPDATE-or-
-- INSERT cursor upsert, and a time-based DELETE in one call -- past the "one simple statement type" subset
-- this codebase's natively-compiled procs stick to. WITH (SNAPSHOT) table hints make the required isolation
-- level explicit regardless of the caller's ambient transaction/autocommit state.
--
-- Transaction-wrapping note (procs-runtime audit, basse severite): the cursor upsert and the reap DELETE below
-- are two separate autocommit statements, deliberately NOT wrapped in an explicit BEGIN TRANSACTION despite
-- this codebase's own "more than one write statement wraps in BEGIN TRAN/COMMIT TRAN" convention. Evaluated
-- and confirmed this is not a correctness gap: the cursor upsert only ever advances forward and the reap
-- DELETE re-evaluates the same flat CreatedAtUtc predicate on every call, so both are independently idempotent
-- -- a mid-batch failure just means the next poll cycle naturally catches up, with no invariant spanning the
-- two writes. Left un-wrapped on purpose: it interacts BETTER with the caller's retry-on-conflict handling
-- (41302/41305/41325 under SNAPSHOT) than wrapping would -- if only the DELETE conflicts, retrying just
-- re-runs the DELETE without redoing an already-committed cursor advance, whereas one explicit transaction
-- would roll back and redo both writes on any conflict. Do not add a BEGIN TRANSACTION here without
-- re-deriving this reasoning first.
CREATE OR ALTER PROCEDURE runtime.usp_SocialCrossShardRelay_Poll @ShardId TINYINT,
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
    FROM runtime.SocialCrossShardRelayCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT @NewLastRelayId = MAX(RelayId)
    FROM runtime.SocialCrossShardRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND TargetShardId = @ShardId;

    SELECT RelayId,
           Kind,
           MessageType,
           Accepted,
           ReasonCode,
           SourceShardId,
           SourceCharacterId,
           SourceAvatarName,
           TargetShardId,
           TargetCharacterId,
           AskRelayId
    FROM runtime.SocialCrossShardRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND TargetShardId = @ShardId
    ORDER BY RelayId ASC;

    IF @NewLastRelayId IS NOT NULL
        BEGIN
            UPDATE runtime.SocialCrossShardRelayCursor WITH (SNAPSHOT)
            SET LastRelayId = @NewLastRelayId
            WHERE ShardId = @ShardId;

            IF @@ROWCOUNT = 0
                INSERT INTO runtime.SocialCrossShardRelayCursor (ShardId, LastRelayId)
                VALUES (@ShardId, @NewLastRelayId);
        END;

    DELETE
    FROM runtime.SocialCrossShardRelay WITH (SNAPSHOT)
    WHERE CreatedAtUtc <= DATEADD(SECOND, -@RetentionSeconds, SYSUTCDATETIME());
END;

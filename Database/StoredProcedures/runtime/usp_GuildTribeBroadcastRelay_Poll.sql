-- Batched per-shard poll, called once per GuildTribeBroadcastRelayHost cycle: returns every row published by
-- some OTHER shard since this shard's own last poll (excluding SourceShardId = @ShardId -- that shard already
-- delivered locally at publish time), advances this shard's own cursor past everything just returned, and
-- reaps rows older than @RetentionSeconds regardless of shard (see GuildTribeBroadcastRelay's own remarks for
-- why reap is time-based, not cursor-based -- a dead shard's cursor must never pin the table's memory-
-- optimized storage forever).
--
-- Interpreted, not native-compiled, unlike this feature's own single-row Publish proc: this one combines a
-- scalar cursor read, an aggregate MAX, a row-returning SELECT, an UPDATE-or-INSERT cursor upsert, and a
-- time-based DELETE in one call -- past the well-established "one simple statement type" subset this
-- codebase's other natively-compiled procs stick to (see usp_AccountSession_RefreshAndGetKicked/
-- usp_AccountSession_ReapStale's own headers for the same reasoning). WITH (SNAPSHOT) table hints make the
-- required isolation level explicit regardless of the caller's ambient transaction/autocommit state.
CREATE PROCEDURE runtime.usp_GuildTribeBroadcastRelay_Poll @ShardId TINYINT,
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
    FROM runtime.GuildTribeBroadcastCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT @NewLastRelayId = MAX(RelayId)
    FROM runtime.GuildTribeBroadcastRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId;

    SELECT RelayId,
           Kind,
           GuildId,
           Tribe,
           RoleField,
           AvatarName,
           Content,
           HasItemLink,
           ItemLinkIndex,
           ItemLinkActivity,
           ItemLinkValue,
           ItemLinkSocket0,
           ItemLinkSocket1,
           ItemLinkSocket2
    FROM runtime.GuildTribeBroadcastRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId
    ORDER BY RelayId ASC;

    IF @NewLastRelayId IS NOT NULL
        BEGIN
            UPDATE runtime.GuildTribeBroadcastCursor WITH (SNAPSHOT)
            SET LastRelayId = @NewLastRelayId
            WHERE ShardId = @ShardId;

            IF @@ROWCOUNT = 0
                INSERT INTO runtime.GuildTribeBroadcastCursor (ShardId, LastRelayId)
                VALUES (@ShardId, @NewLastRelayId);
        END;

    DELETE
    FROM runtime.GuildTribeBroadcastRelay WITH (SNAPSHOT)
    WHERE CreatedAtUtc <= DATEADD(SECOND, -@RetentionSeconds, SYSUTCDATETIME());
END;

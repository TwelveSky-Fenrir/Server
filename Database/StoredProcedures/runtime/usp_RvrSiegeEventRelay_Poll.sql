-- Batched per-shard poll, called once per RvrSiegeEventRelayHost cycle: returns every row published by some
-- OTHER shard since this shard's own last poll (excluding SourceShardId = @ShardId -- that shard already
-- delivered locally at publish time), advances this shard's own cursor past everything just returned, and
-- reaps rows older than @RetentionSeconds regardless of shard -- same reasoning as
-- usp_GuildTribeBroadcastRelay_Poll/usp_ProxyShopExpirationRelay_Poll's own headers (a dead shard's cursor
-- must never pin the table's memory-optimized storage forever).
--
-- Interpreted, not native-compiled, unlike this feature's own single-row Publish proc: this one combines a
-- scalar cursor read, an aggregate MAX, a row-returning SELECT, an UPDATE-or-INSERT cursor upsert, and a
-- time-based DELETE in one call -- past the well-established "one simple statement type" subset this
-- codebase's other natively-compiled procs stick to, same reasoning as usp_GuildTribeBroadcastRelay_Poll's
-- own header. WITH (SNAPSHOT) table hints make the required isolation level explicit regardless of the
-- caller's ambient transaction/autocommit state.
CREATE PROCEDURE runtime.usp_RvrSiegeEventRelay_Poll @ShardId TINYINT,
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
    FROM runtime.RvrSiegeEventRelayCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT @NewLastRelayId = MAX(RelayId)
    FROM runtime.RvrSiegeEventRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId;

    SELECT RelayId,
           Sort,
           Data
    FROM runtime.RvrSiegeEventRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId
    ORDER BY RelayId ASC;

    IF @NewLastRelayId IS NOT NULL
        BEGIN
            UPDATE runtime.RvrSiegeEventRelayCursor WITH (SNAPSHOT)
            SET LastRelayId = @NewLastRelayId
            WHERE ShardId = @ShardId;

            IF @@ROWCOUNT = 0
                INSERT INTO runtime.RvrSiegeEventRelayCursor (ShardId, LastRelayId)
                VALUES (@ShardId, @NewLastRelayId);
        END;

    DELETE
    FROM runtime.RvrSiegeEventRelay WITH (SNAPSHOT)
    WHERE CreatedAtUtc <= DATEADD(SECOND, -@RetentionSeconds, SYSUTCDATETIME());
END;

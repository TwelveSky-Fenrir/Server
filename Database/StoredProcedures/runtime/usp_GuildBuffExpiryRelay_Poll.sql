-- Batched per-shard poll, called once per GuildBuffExpiryRelayHost cycle: returns every row published by some
-- OTHER shard since this shard's own last poll (excluding SourceShardId = @ShardId -- that shard already
-- delivered locally at publish time), advances this shard's own cursor past everything just returned, and
-- reaps rows older than @RetentionSeconds regardless of shard. Same shape/reasoning as
-- usp_GuildTribeBroadcastRelay_Poll -- see that proc's own header for why this combines a scalar cursor read,
-- an aggregate MAX, a row-returning SELECT, an UPDATE-or-INSERT cursor upsert, and a time-based DELETE in one
-- interpreted (not natively-compiled) call, and why WITH (SNAPSHOT) table hints make the required isolation
-- level explicit regardless of the caller's ambient transaction/autocommit state.
CREATE PROCEDURE runtime.usp_GuildBuffExpiryRelay_Poll @ShardId TINYINT,
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
    FROM runtime.GuildBuffExpiryCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT @NewLastRelayId = MAX(RelayId)
    FROM runtime.GuildBuffExpiryRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId;

    SELECT RelayId,
           GuildId,
           NewBuffTime
    FROM runtime.GuildBuffExpiryRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId
    ORDER BY RelayId ASC;

    IF @NewLastRelayId IS NOT NULL
        BEGIN
            UPDATE runtime.GuildBuffExpiryCursor WITH (SNAPSHOT)
            SET LastRelayId = @NewLastRelayId
            WHERE ShardId = @ShardId;

            IF @@ROWCOUNT = 0
                INSERT INTO runtime.GuildBuffExpiryCursor (ShardId, LastRelayId)
                VALUES (@ShardId, @NewLastRelayId);
        END;

    DELETE
    FROM runtime.GuildBuffExpiryRelay WITH (SNAPSHOT)
    WHERE CreatedAtUtc <= DATEADD(SECOND, -@RetentionSeconds, SYSUTCDATETIME());
END;

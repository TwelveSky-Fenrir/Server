CREATE OR ALTER PROCEDURE runtime.usp_RvrSiegeEventRelay_Poll @ShardId TINYINT,
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

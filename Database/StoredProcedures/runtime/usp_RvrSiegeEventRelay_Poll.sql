CREATE OR ALTER PROCEDURE runtime.usp_RvrSiegeEventRelay_Poll @ShardId TINYINT,
                                                              @RetentionSeconds INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE @LastRelayId BIGINT = 0;
    SELECT @LastRelayId = LastRelayId
    FROM runtime.RvrSiegeEventRelayCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT RelayId,
           Sort,
           Data
    FROM runtime.RvrSiegeEventRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId
    ORDER BY RelayId ASC;

    DELETE
    FROM runtime.RvrSiegeEventRelay WITH (SNAPSHOT)
    WHERE CreatedAtUtc <= DATEADD(SECOND, -@RetentionSeconds, SYSUTCDATETIME());
END;

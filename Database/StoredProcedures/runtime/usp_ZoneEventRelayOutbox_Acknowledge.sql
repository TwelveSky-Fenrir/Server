CREATE OR ALTER PROCEDURE runtime.usp_ZoneEventRelayOutbox_Acknowledge @OutboxId BIGINT,
                                                                        @SourceShardId TINYINT,
                                                                        @LeaseId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @OutboxId <= 0 OR @SourceShardId = 0 OR @LeaseId IS NULL OR
       @LeaseId = '00000000-0000-0000-0000-000000000000'
        THROW 51120, 'Zone event relay outbox acknowledgement parameters are invalid.', 1;

    DECLARE @Acknowledged BIT = 0;

    BEGIN TRANSACTION;

    UPDATE runtime.ZoneEventRelayOutbox WITH (UPDLOCK, HOLDLOCK)
    SET PublishStatus = 2,
        LeaseId = NULL,
        LeaseExpiresAtUtc = NULL,
        PublishedAtUtc = COALESCE(PublishedAtUtc, SYSUTCDATETIME())
    WHERE OutboxId = @OutboxId
      AND SourceShardId = @SourceShardId
      AND PublishStatus = 1
      AND LeaseId = @LeaseId;

    IF @@ROWCOUNT = 1
        SET @Acknowledged = 1;
    ELSE IF EXISTS
    (
        SELECT 1
        FROM runtime.ZoneEventRelayOutbox WITH (UPDLOCK, HOLDLOCK)
        WHERE OutboxId = @OutboxId
          AND SourceShardId = @SourceShardId
          AND PublishStatus = 2
    )
        SET @Acknowledged = 1;

    COMMIT TRANSACTION;

    SELECT @Acknowledged AS Acknowledged;
END;

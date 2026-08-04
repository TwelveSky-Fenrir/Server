CREATE OR ALTER PROCEDURE runtime.usp_ZoneEventRelayOutbox_Enqueue @SourceShardId TINYINT,
                                                                   @Sort INT,
                                                                   @Data VARBINARY(130),
                                                                   @OperationId UNIQUEIDENTIFIER,
                                                                   @CorrelationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @SourceShardId = 0 OR DATALENGTH(@Data) <> 130 OR @OperationId IS NULL OR @CorrelationId IS NULL OR
       @OperationId = '00000000-0000-0000-0000-000000000000' OR
       @CorrelationId = '00000000-0000-0000-0000-000000000000'
        THROW 51100, 'Zone event relay outbox parameters are invalid.', 1;

    DECLARE @AuthenticatedSource NVARCHAR(128) = ORIGINAL_LOGIN();
    DECLARE @OutboxId BIGINT;
    DECLARE @ActiveCount BIGINT;
    DECLARE @IsAccepted BIT = 1;
    DECLARE @WasEnqueued BIT = 0;
    DECLARE @NowUtc DATETIME2(3) = SYSUTCDATETIME();

    BEGIN TRANSACTION;

    SELECT @OutboxId = OutboxId
    FROM runtime.ZoneEventRelayOutbox
    WITH (UPDLOCK, HOLDLOCK)
    WHERE OperationId = @OperationId;

    IF @OutboxId IS NOT NULL
        BEGIN
            IF NOT EXISTS
                (SELECT 1
                 FROM runtime.ZoneEventRelayOutbox
                 WHERE OutboxId = @OutboxId
                   AND AuthenticatedSource = @AuthenticatedSource
                   AND SourceShardId = @SourceShardId
                   AND Sort = @Sort
                   AND Data = @Data
                   AND CorrelationId = @CorrelationId)
                THROW 51101, 'Zone event relay operation identifier was reused with a different envelope.', 1;
        END
    ELSE
        BEGIN
            ;
            WITH ExpiredPublishedEvents AS
                     (SELECT TOP (64) OutboxId
                      FROM runtime.ZoneEventRelayOutbox
                      WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK, ROWLOCK, INDEX (IX_ZoneEventRelayOutbox_PublishedRetention))
                      WHERE PublishStatus = 2
                        AND PublishedAtUtc <= DATEADD(HOUR, -24, @NowUtc)
                      ORDER BY PublishedAtUtc, OutboxId)
            DELETE
            FROM ExpiredPublishedEvents;

            SELECT @ActiveCount = COUNT_BIG(*)
            FROM runtime.ZoneEventRelayOutbox
            WITH (TABLOCKX, HOLDLOCK)
            WHERE PublishStatus IN (0, 1);

            IF @ActiveCount >= 256
                BEGIN
                    SET @OutboxId = 0;
                    SET @IsAccepted = 0;
                END
            ELSE
                BEGIN
                    INSERT INTO runtime.ZoneEventRelayOutbox
                    (AuthenticatedSource, SourceShardId, Sort, Data, OperationId, CorrelationId, PublishStatus,
                     AttemptCount, NextAttemptAtUtc, LastAttemptedAtUtc, LeaseId, LeaseExpiresAtUtc, PublishedAtUtc,
                     CreatedAtUtc)
                    VALUES (@AuthenticatedSource, @SourceShardId, @Sort, @Data, @OperationId, @CorrelationId, 0, 0,
                            @NowUtc, NULL, NULL, NULL, NULL, @NowUtc);

                    SET @OutboxId = SCOPE_IDENTITY();
                    SET @WasEnqueued = 1;
                END;
        END;

    COMMIT TRANSACTION;

    SELECT @OutboxId    AS OutboxId,
           @IsAccepted  AS IsAccepted,
           @WasEnqueued AS WasEnqueued;
END;

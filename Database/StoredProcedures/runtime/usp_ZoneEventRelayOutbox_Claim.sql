CREATE OR ALTER PROCEDURE runtime.usp_ZoneEventRelayOutbox_Claim @SourceShardId TINYINT,
                                                                 @LeaseId UNIQUEIDENTIFIER,
                                                                 @MaximumCount INT,
                                                                 @LeaseSeconds INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @SourceShardId = 0 OR @LeaseId IS NULL OR @LeaseId = '00000000-0000-0000-0000-000000000000' OR
       @MaximumCount NOT BETWEEN 1 AND 64 OR @LeaseSeconds NOT BETWEEN 5 AND 300
        THROW 51110, 'Zone event relay outbox claim parameters are invalid.', 1;

    DECLARE @NowUtc DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @LeaseExpiresAtUtc DATETIME2(3) = DATEADD(SECOND, @LeaseSeconds, @NowUtc);

    DECLARE @Claimed TABLE
                     (
                         OutboxId      BIGINT           NOT NULL,
                         SourceShardId TINYINT          NOT NULL,
                         Sort          INT              NOT NULL,
                         Data          VARBINARY(130)   NOT NULL,
                         OperationId   UNIQUEIDENTIFIER NOT NULL,
                         CorrelationId UNIQUEIDENTIFIER NOT NULL,
                         AttemptCount  INT              NOT NULL
                     );

    BEGIN TRANSACTION;
    ;
    WITH Claimable AS
             (SELECT TOP (@MaximumCount) *
              FROM runtime.ZoneEventRelayOutbox
              WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK, ROWLOCK, INDEX (IX_ZoneEventRelayOutbox_Claim))
              WHERE SourceShardId = @SourceShardId
                AND (
                  (PublishStatus = 0 AND NextAttemptAtUtc <= @NowUtc) OR
                  (PublishStatus = 1 AND LeaseExpiresAtUtc <= @NowUtc)
                  )
              ORDER BY OutboxId ASC)
    UPDATE Claimable
    SET PublishStatus      = 1,
        AttemptCount       = AttemptCount + 1,
        LastAttemptedAtUtc = @NowUtc,
        LeaseId            = @LeaseId,
        LeaseExpiresAtUtc  = @LeaseExpiresAtUtc
    OUTPUT INSERTED.OutboxId,
           INSERTED.SourceShardId,
           INSERTED.Sort,
           INSERTED.Data,
           INSERTED.OperationId,
           INSERTED.CorrelationId,
           INSERTED.AttemptCount
        INTO @Claimed;

    COMMIT TRANSACTION;

    SELECT OutboxId,
           SourceShardId,
           Sort,
           Data,
           OperationId,
           CorrelationId,
           AttemptCount
    FROM @Claimed
    ORDER BY OutboxId ASC;
END;

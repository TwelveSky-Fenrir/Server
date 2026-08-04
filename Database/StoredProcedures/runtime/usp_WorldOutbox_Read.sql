CREATE OR ALTER PROCEDURE runtime.usp_WorldOutbox_Read @DestinationShardId TINYINT,
                                                       @DeliveryLeaseId UNIQUEIDENTIFIER,
                                                       @MaximumCount INT,
                                                       @LeaseSeconds INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @DestinationShardId IS NULL OR @DeliveryLeaseId IS NULL OR
       @DeliveryLeaseId = '00000000-0000-0000-0000-000000000000' OR @MaximumCount NOT BETWEEN 1 AND 256 OR
       @LeaseSeconds NOT BETWEEN 5 AND 300
        THROW 51010, 'World outbox read limits are invalid.', 1;

    DECLARE @NowUtc DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @LeaseExpiresAtUtc DATETIME2(3) = DATEADD(SECOND, @LeaseSeconds, @NowUtc);
    DECLARE @Claimed TABLE
                     (
                         OutboxId            BIGINT           NOT NULL,
                         AuthenticatedSource NVARCHAR(128)    NOT NULL,
                         SourceShardId       TINYINT          NOT NULL,
                         SourceSequence      BIGINT           NOT NULL,
                         DestinationShardId  TINYINT          NOT NULL,
                         PayloadCategory     TINYINT          NOT NULL,
                         Payload             VARBINARY(4096)  NOT NULL,
                         PayloadHash         BINARY(32)       NOT NULL,
                         CorrelationId       UNIQUEIDENTIFIER NOT NULL,
                         IdempotencyKey      UNIQUEIDENTIFIER NOT NULL,
                         AttemptCount        SMALLINT         NOT NULL
                     );

    BEGIN TRANSACTION;

    INSERT INTO @Claimed
    (OutboxId, AuthenticatedSource, SourceShardId, SourceSequence, DestinationShardId, PayloadCategory, Payload,
     PayloadHash, CorrelationId, IdempotencyKey, AttemptCount)
    SELECT OutboxId,
           AuthenticatedSource,
           SourceShardId,
           SourceSequence,
           DestinationShardId,
           PayloadCategory,
           Payload,
           PayloadHash,
           CorrelationId,
           IdempotencyKey,
           AttemptCount
    FROM runtime.WorldOutbox
    WITH (UPDLOCK, HOLDLOCK)
    WHERE DestinationShardId = @DestinationShardId
      AND DeliveryStatus = 1
      AND DeliveryLeaseId = @DeliveryLeaseId
      AND LeaseExpiresAtUtc > @NowUtc;

    IF NOT EXISTS (SELECT 1 FROM @Claimed)
        BEGIN
            UPDATE runtime.WorldOutbox WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK, ROWLOCK)
            SET DeliveryStatus    = 3,
                DeliveryLeaseId   = NULL,
                LeaseExpiresAtUtc = NULL
            WHERE DestinationShardId = @DestinationShardId
              AND DeliveryStatus = 1
              AND LeaseExpiresAtUtc <= @NowUtc
              AND AttemptCount >= 25;
            ;
            WITH Claimable AS
                     (SELECT TOP (@MaximumCount) *
                      FROM runtime.WorldOutbox
                      WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK, ROWLOCK, INDEX (IX_WorldOutbox_Claim))
                      WHERE DestinationShardId = @DestinationShardId
                        AND (
                          (DeliveryStatus = 0 AND NextAttemptAtUtc <= @NowUtc) OR
                          (DeliveryStatus = 1 AND LeaseExpiresAtUtc <= @NowUtc AND AttemptCount < 25)
                          )
                      ORDER BY OutboxId ASC)
            UPDATE Claimable
            SET DeliveryStatus     = 1,
                AttemptCount       = AttemptCount + 1,
                LastAttemptedAtUtc = @NowUtc,
                DeliveryLeaseId    = @DeliveryLeaseId,
                LeaseExpiresAtUtc  = @LeaseExpiresAtUtc
            OUTPUT inserted.OutboxId,
                   inserted.AuthenticatedSource,
                   inserted.SourceShardId,
                   inserted.SourceSequence,
                   inserted.DestinationShardId,
                   inserted.PayloadCategory,
                   inserted.Payload,
                   inserted.PayloadHash,
                   inserted.CorrelationId,
                   inserted.IdempotencyKey,
                   inserted.AttemptCount
                INTO @Claimed;
        END;

    COMMIT TRANSACTION;

    SELECT OutboxId,
           AuthenticatedSource,
           SourceShardId,
           SourceSequence,
           DestinationShardId,
           PayloadCategory,
           Payload,
           PayloadHash,
           CorrelationId,
           IdempotencyKey,
           AttemptCount
    FROM @Claimed
    ORDER BY OutboxId ASC;
END;

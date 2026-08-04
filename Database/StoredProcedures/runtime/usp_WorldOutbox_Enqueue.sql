CREATE OR ALTER PROCEDURE runtime.usp_WorldOutbox_Enqueue @SourceShardId TINYINT,
                                                          @SourceSequence BIGINT,
                                                          @DestinationShardId TINYINT,
                                                          @PayloadCategory TINYINT,
                                                          @Payload VARBINARY(4096),
                                                          @PayloadHash BINARY(32),
                                                          @CorrelationId UNIQUEIDENTIFIER,
                                                          @IdempotencyKey UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @SourceShardId IS NULL OR @DestinationShardId IS NULL OR @SourceSequence IS NULL OR @SourceSequence <= 0 OR
       @SourceShardId = @DestinationShardId
        THROW 51000, 'World outbox source sequence and shard routing are invalid.', 1;

    IF @PayloadCategory IS NULL OR @PayloadCategory NOT BETWEEN 1 AND 6 OR @Payload IS NULL OR
       DATALENGTH(@Payload) NOT BETWEEN 1 AND 4096
        THROW 51001, 'World outbox payload category or length is invalid.', 1;

    IF @PayloadHash IS NULL OR HASHBYTES('SHA2_256', @Payload) <> @PayloadHash
        THROW 51002, 'World outbox payload hash does not match the supplied payload.', 1;

    IF @CorrelationId IS NULL OR @CorrelationId = '00000000-0000-0000-0000-000000000000' OR
       @IdempotencyKey IS NULL OR @IdempotencyKey = '00000000-0000-0000-0000-000000000000'
        THROW 51005, 'World outbox correlation and idempotency identifiers are required.', 1;

    DECLARE @AuthenticatedSource NVARCHAR(128) = ORIGINAL_LOGIN();
    DECLARE @OutboxId BIGINT;
    DECLARE @ExistingSourceSequence BIGINT;
    DECLARE @WasEnqueued BIT = 0;

    BEGIN TRANSACTION;

    SELECT @OutboxId = OutboxId
    FROM runtime.WorldOutbox
    WITH (UPDLOCK, HOLDLOCK)
    WHERE IdempotencyKey = @IdempotencyKey;

    IF @OutboxId IS NOT NULL
        BEGIN
            IF NOT EXISTS
                (SELECT 1
                 FROM runtime.WorldOutbox
                 WHERE OutboxId = @OutboxId
                   AND AuthenticatedSource = @AuthenticatedSource
                   AND SourceShardId = @SourceShardId
                   AND SourceSequence = @SourceSequence
                   AND DestinationShardId = @DestinationShardId
                   AND PayloadCategory = @PayloadCategory
                   AND Payload = @Payload
                   AND PayloadHash = @PayloadHash
                   AND CorrelationId = @CorrelationId)
                THROW 51003, 'World outbox idempotency key was reused with a different envelope.', 1;
        END
    ELSE
        BEGIN
            SELECT @ExistingSourceSequence = MAX(SourceSequence)
            FROM runtime.WorldOutbox
            WITH (UPDLOCK, HOLDLOCK, INDEX (UQ_WorldOutbox_SourceSequence))
            WHERE AuthenticatedSource = @AuthenticatedSource
              AND SourceShardId = @SourceShardId;

            IF @ExistingSourceSequence IS NOT NULL AND @SourceSequence <= @ExistingSourceSequence
                THROW 51004, 'World outbox source sequence must increase monotonically.', 1;

            INSERT INTO runtime.WorldOutbox
            (AuthenticatedSource, SourceShardId, SourceSequence, DestinationShardId, PayloadCategory, Payload,
             PayloadHash, CorrelationId, IdempotencyKey, DeliveryStatus, AttemptCount, NextAttemptAtUtc,
             LastAttemptedAtUtc, DeliveryLeaseId, LeaseExpiresAtUtc, AcknowledgedAtUtc, AcknowledgedByShardId,
             CreatedAtUtc)
            VALUES (@AuthenticatedSource, @SourceShardId, @SourceSequence, @DestinationShardId, @PayloadCategory,
                    @Payload,
                    @PayloadHash, @CorrelationId, @IdempotencyKey, 0, 0, SYSUTCDATETIME(), NULL, NULL, NULL, NULL, NULL,
                    SYSUTCDATETIME());

            SET @OutboxId = SCOPE_IDENTITY();
            SET @WasEnqueued = 1;
        END;

    COMMIT TRANSACTION;

    SELECT @OutboxId AS OutboxId, @WasEnqueued AS WasEnqueued;
END;

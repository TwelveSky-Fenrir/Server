CREATE OR ALTER PROCEDURE runtime.usp_WorldInbox_Apply @OutboxId BIGINT,
                                                       @DestinationShardId TINYINT,
                                                       @DeliveryLeaseId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @OutboxId IS NULL OR @OutboxId <= 0 OR @DestinationShardId IS NULL OR @DeliveryLeaseId IS NULL OR
       @DeliveryLeaseId = '00000000-0000-0000-0000-000000000000'
        THROW 51020, 'World inbox receipt parameters are invalid.', 1;

    DECLARE @InboxId BIGINT;
    DECLARE @EffectCompletedAtUtc DATETIME2(3);
    DECLARE @WasReceived BIT = 0;
    DECLARE @DeliveryStatus TINYINT;
    DECLARE @ExistingDestinationShardId TINYINT;
    DECLARE @ExistingLeaseId UNIQUEIDENTIFIER;

    BEGIN TRANSACTION;

    SELECT @DeliveryStatus = DeliveryStatus,
           @ExistingDestinationShardId = DestinationShardId,
           @ExistingLeaseId = DeliveryLeaseId
    FROM runtime.WorldOutbox
    WITH (UPDLOCK, HOLDLOCK)
    WHERE OutboxId = @OutboxId;

    IF @DeliveryStatus IS NULL
        THROW 51021, 'World outbox message was not found.', 1;

    IF @ExistingDestinationShardId <> @DestinationShardId
        THROW 51022, 'World inbox receipt attempted from the wrong destination shard.', 1;

    SELECT @InboxId = InboxId,
           @EffectCompletedAtUtc = EffectCompletedAtUtc
    FROM runtime.WorldInbox
    WITH (UPDLOCK, HOLDLOCK)
    WHERE OutboxId = @OutboxId;

    IF @DeliveryStatus = 2
        BEGIN
            IF @InboxId IS NULL
                THROW 51023, 'World outbox was acknowledged without a durable inbox receipt.', 1;
        END
    ELSE
        IF @DeliveryStatus <> 1 OR @ExistingLeaseId <> @DeliveryLeaseId
            THROW 51024, 'World inbox receipt requires the current delivery lease.', 1;

    IF @InboxId IS NULL
        BEGIN
            IF NOT EXISTS
                (SELECT 1
                 FROM runtime.WorldOutbox
                 WHERE OutboxId = @OutboxId
                   AND DATALENGTH(Payload) BETWEEN 1 AND 4096
                   AND HASHBYTES('SHA2_256', Payload) = PayloadHash)
                THROW 51025, 'World outbox payload hash or length is invalid.', 1;

            INSERT INTO runtime.WorldInbox
            (OutboxId, AuthenticatedSource, SourceShardId, SourceSequence, DestinationShardId, PayloadCategory,
             Payload, PayloadHash, CorrelationId, IdempotencyKey, ReceivedAtUtc, EffectCompletedAtUtc)
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
                   SYSUTCDATETIME(),
                   NULL
            FROM runtime.WorldOutbox
            WHERE OutboxId = @OutboxId;

            SET @InboxId = SCOPE_IDENTITY();
            SET @WasReceived = 1;
        END;

    COMMIT TRANSACTION;

    SELECT @InboxId                                                               AS InboxId,
           @WasReceived                                                           AS WasReceived,
           CAST(CASE WHEN @EffectCompletedAtUtc IS NULL THEN 0 ELSE 1 END AS BIT) AS IsEffectCompleted;
END;

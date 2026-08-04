CREATE OR ALTER PROCEDURE runtime.usp_WorldInbox_Acknowledge @OutboxId BIGINT,
                                                             @DestinationShardId TINYINT,
                                                             @DeliveryLeaseId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @OutboxId IS NULL OR @OutboxId <= 0 OR @DestinationShardId IS NULL OR @DeliveryLeaseId IS NULL OR
       @DeliveryLeaseId = '00000000-0000-0000-0000-000000000000'
        THROW 51030, 'World inbox acknowledgement parameters are invalid.', 1;

    DECLARE @InboxId BIGINT;
    DECLARE @EffectCompletedAtUtc DATETIME2(3);
    DECLARE @DeliveryStatus TINYINT;
    DECLARE @ExistingDestinationShardId TINYINT;
    DECLARE @ExistingLeaseId UNIQUEIDENTIFIER;
    DECLARE @AcknowledgedByShardId TINYINT;
    DECLARE @Acknowledged BIT = 0;

    BEGIN TRANSACTION;

    SELECT @DeliveryStatus = DeliveryStatus,
           @ExistingDestinationShardId = DestinationShardId,
           @ExistingLeaseId = DeliveryLeaseId,
           @AcknowledgedByShardId = AcknowledgedByShardId
    FROM runtime.WorldOutbox WITH (UPDLOCK, HOLDLOCK)
    WHERE OutboxId = @OutboxId;

    IF @DeliveryStatus IS NULL
        THROW 51031, 'World outbox message was not found.', 1;

    IF @ExistingDestinationShardId <> @DestinationShardId
        THROW 51032, 'World inbox acknowledgement attempted from the wrong destination shard.', 1;

    SELECT @InboxId = InboxId,
           @EffectCompletedAtUtc = EffectCompletedAtUtc
    FROM runtime.WorldInbox WITH (UPDLOCK, HOLDLOCK)
    WHERE OutboxId = @OutboxId;

    IF @InboxId IS NULL
        THROW 51033, 'World inbox acknowledgement requires a durable receipt.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM runtime.WorldInboxEffectOperation
        WHERE InboxId = @InboxId
          AND OutboxId = @OutboxId
          AND DestinationShardId = @DestinationShardId
          AND OperationKey =
              (SELECT IdempotencyKey FROM runtime.WorldInbox WHERE InboxId = @InboxId)
    )
        THROW 51035, 'World inbox acknowledgement requires a durable idempotent local effect.', 1;

    IF @DeliveryStatus = 2
    BEGIN
        IF @AcknowledgedByShardId <> @DestinationShardId OR @EffectCompletedAtUtc IS NULL
            THROW 51034, 'World outbox acknowledgement is inconsistent with the durable inbox.', 1;

        SET @Acknowledged = 1;
    END
    ELSE IF @DeliveryStatus = 1 AND @ExistingLeaseId = @DeliveryLeaseId
    BEGIN
        UPDATE runtime.WorldInbox
        SET EffectCompletedAtUtc = COALESCE(EffectCompletedAtUtc, SYSUTCDATETIME())
        WHERE InboxId = @InboxId;

        UPDATE runtime.WorldOutbox
        SET DeliveryStatus = 2,
            DeliveryLeaseId = NULL,
            LeaseExpiresAtUtc = NULL,
            AcknowledgedAtUtc = SYSUTCDATETIME(),
            AcknowledgedByShardId = @DestinationShardId
        WHERE OutboxId = @OutboxId;

        SET @Acknowledged = 1;
    END;

    COMMIT TRANSACTION;

    SELECT @Acknowledged AS Acknowledged;
END;

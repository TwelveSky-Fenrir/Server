CREATE OR ALTER PROCEDURE runtime.usp_WorldInboxEffect_ApplyWorldStateHighTribe @OutboxId BIGINT,
                                                                                  @DestinationShardId TINYINT,
                                                                                  @OperationKey UNIQUEIDENTIFIER,
                                                                                  @Payload VARBINARY(3),
                                                                                  @HighTribe TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @OutboxId IS NULL OR @OutboxId <= 0 OR @DestinationShardId IS NULL OR @OperationKey IS NULL OR
       @OperationKey = '00000000-0000-0000-0000-000000000000' OR
       @Payload IS NULL OR DATALENGTH(@Payload) <> 3 OR SUBSTRING(@Payload, 1, 1) <> 0x01 OR
       SUBSTRING(@Payload, 2, 1) <> 0x01 OR
       (@HighTribe IS NOT NULL AND @HighTribe NOT BETWEEN 0 AND 3) OR
       (@HighTribe IS NULL AND SUBSTRING(@Payload, 3, 1) <> 0xFF) OR
       (@HighTribe IS NOT NULL AND CONVERT(TINYINT, SUBSTRING(@Payload, 3, 1)) <> @HighTribe)
        THROW 51040, 'World-state inbox effect parameters are invalid.', 1;

    DECLARE @InboxId BIGINT;
    DECLARE @PayloadHash BINARY(32);
    DECLARE @PayloadCategory TINYINT;
    DECLARE @InboxOperationKey UNIQUEIDENTIFIER;
    DECLARE @ExistingOperationKey UNIQUEIDENTIFIER;
    DECLARE @WasApplied BIT = 0;

    BEGIN TRANSACTION;

    SELECT @InboxId = InboxId,
           @PayloadHash = PayloadHash,
           @PayloadCategory = PayloadCategory,
           @InboxOperationKey = IdempotencyKey
    FROM runtime.WorldInbox WITH (UPDLOCK, HOLDLOCK)
    WHERE OutboxId = @OutboxId
      AND DestinationShardId = @DestinationShardId;

    IF @InboxId IS NULL
        THROW 51041, 'World-state inbox effect requires a durable inbox receipt.', 1;

    IF @PayloadCategory <> 1 OR @InboxOperationKey <> @OperationKey OR @PayloadHash <> HASHBYTES('SHA2_256', @Payload)
        THROW 51042, 'World-state inbox effect does not match its durable envelope.', 1;

    SELECT @ExistingOperationKey = OperationKey
    FROM runtime.WorldInboxEffectOperation WITH (UPDLOCK, HOLDLOCK)
    WHERE InboxId = @InboxId;

    IF @ExistingOperationKey IS NULL
    BEGIN
        UPDATE game.WorldState
        SET HighTribe = @HighTribe,
            Revision = Revision + 1,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE Id = 1;

        IF @@ROWCOUNT <> 1
            THROW 51043, 'World-state singleton is missing.', 1;

        INSERT INTO runtime.WorldInboxEffectOperation
            (InboxId, OutboxId, DestinationShardId, OperationKey, PayloadCategory, PayloadHash)
        VALUES
            (@InboxId, @OutboxId, @DestinationShardId, @OperationKey, @PayloadCategory, @PayloadHash);

        SET @WasApplied = 1;
    END
    ELSE IF @ExistingOperationKey <> @OperationKey
        THROW 51044, 'World-state inbox effect operation key conflicts with the durable receipt.', 1;

    COMMIT TRANSACTION;

    SELECT @WasApplied AS WasApplied;
END;

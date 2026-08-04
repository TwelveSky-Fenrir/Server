CREATE TABLE runtime.WorldOutbox
(
    OutboxId              BIGINT IDENTITY (1,1) NOT NULL,
    AuthenticatedSource   NVARCHAR(128)          NOT NULL,
    SourceShardId         TINYINT                NOT NULL,
    SourceSequence        BIGINT                 NOT NULL,
    DestinationShardId    TINYINT                NOT NULL,
    PayloadCategory       TINYINT                NOT NULL,
    Payload               VARBINARY(4096)        NOT NULL,
    PayloadHash           BINARY(32)             NOT NULL,
    CorrelationId         UNIQUEIDENTIFIER       NOT NULL,
    IdempotencyKey        UNIQUEIDENTIFIER       NOT NULL,
    DeliveryStatus        TINYINT                NOT NULL,
    AttemptCount          SMALLINT               NOT NULL,
    NextAttemptAtUtc      DATETIME2(3)           NOT NULL,
    LastAttemptedAtUtc    DATETIME2(3)           NULL,
    DeliveryLeaseId       UNIQUEIDENTIFIER       NULL,
    LeaseExpiresAtUtc     DATETIME2(3)           NULL,
    AcknowledgedAtUtc     DATETIME2(3)           NULL,
    AcknowledgedByShardId TINYINT                NULL,
    CreatedAtUtc          DATETIME2(3)           NOT NULL,
    CONSTRAINT PK_WorldOutbox PRIMARY KEY CLUSTERED (OutboxId),
    CONSTRAINT UQ_WorldOutbox_IdempotencyKey UNIQUE NONCLUSTERED (IdempotencyKey),
    CONSTRAINT UQ_WorldOutbox_SourceSequence UNIQUE NONCLUSTERED
        (AuthenticatedSource, SourceShardId, SourceSequence),
    CONSTRAINT CK_WorldOutbox_SourceSequence CHECK (SourceSequence > 0),
    CONSTRAINT CK_WorldOutbox_DifferentShards CHECK (SourceShardId <> DestinationShardId),
    CONSTRAINT CK_WorldOutbox_PayloadCategory CHECK (PayloadCategory BETWEEN 1 AND 6),
    CONSTRAINT CK_WorldOutbox_PayloadLength CHECK (DATALENGTH(Payload) BETWEEN 1 AND 4096),
    CONSTRAINT CK_WorldOutbox_AttemptCount CHECK (AttemptCount BETWEEN 0 AND 25),
    CONSTRAINT CK_WorldOutbox_DeliveryStatus CHECK (DeliveryStatus BETWEEN 0 AND 3),
    CONSTRAINT CK_WorldOutbox_StatusFields CHECK
        (
            (DeliveryStatus = 0 AND DeliveryLeaseId IS NULL AND LeaseExpiresAtUtc IS NULL AND
             AcknowledgedAtUtc IS NULL AND AcknowledgedByShardId IS NULL) OR
            (DeliveryStatus = 1 AND DeliveryLeaseId IS NOT NULL AND LeaseExpiresAtUtc IS NOT NULL AND
             AcknowledgedAtUtc IS NULL AND AcknowledgedByShardId IS NULL) OR
            (DeliveryStatus = 2 AND DeliveryLeaseId IS NULL AND LeaseExpiresAtUtc IS NULL AND
             AcknowledgedAtUtc IS NOT NULL AND AcknowledgedByShardId IS NOT NULL) OR
            (DeliveryStatus = 3 AND DeliveryLeaseId IS NULL AND LeaseExpiresAtUtc IS NULL AND
             AcknowledgedAtUtc IS NULL AND AcknowledgedByShardId IS NULL)
        ),
    INDEX IX_WorldOutbox_Claim NONCLUSTERED
        (DestinationShardId, DeliveryStatus, NextAttemptAtUtc, LeaseExpiresAtUtc, OutboxId)
);

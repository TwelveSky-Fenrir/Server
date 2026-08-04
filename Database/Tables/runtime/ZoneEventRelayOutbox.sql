CREATE TABLE runtime.ZoneEventRelayOutbox
(
    OutboxId            BIGINT IDENTITY (1,1) NOT NULL,
    AuthenticatedSource NVARCHAR(128)          NOT NULL,
    SourceShardId       TINYINT                NOT NULL,
    Sort                INT                    NOT NULL,
    Data                VARBINARY(130)         NOT NULL,
    OperationId         UNIQUEIDENTIFIER       NOT NULL,
    CorrelationId       UNIQUEIDENTIFIER       NOT NULL,
    PublishStatus       TINYINT                NOT NULL,
    AttemptCount        INT                    NOT NULL,
    NextAttemptAtUtc    DATETIME2(3)           NOT NULL,
    LastAttemptedAtUtc  DATETIME2(3)           NULL,
    LeaseId             UNIQUEIDENTIFIER       NULL,
    LeaseExpiresAtUtc   DATETIME2(3)           NULL,
    PublishedAtUtc      DATETIME2(3)           NULL,
    CreatedAtUtc        DATETIME2(3)           NOT NULL,
    CONSTRAINT PK_ZoneEventRelayOutbox PRIMARY KEY CLUSTERED (OutboxId),
    CONSTRAINT UQ_ZoneEventRelayOutbox_OperationId UNIQUE NONCLUSTERED (OperationId),
    CONSTRAINT UQ_ZoneEventRelayOutbox_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    CONSTRAINT CK_ZoneEventRelayOutbox_SourceShardId CHECK (SourceShardId > 0),
    CONSTRAINT CK_ZoneEventRelayOutbox_DataLength CHECK (DATALENGTH(Data) = 130),
    CONSTRAINT CK_ZoneEventRelayOutbox_AttemptCount CHECK (AttemptCount >= 0),
    CONSTRAINT CK_ZoneEventRelayOutbox_PublishStatus CHECK (PublishStatus BETWEEN 0 AND 2),
    CONSTRAINT CK_ZoneEventRelayOutbox_StatusFields CHECK
        (
            (PublishStatus = 0 AND LeaseId IS NULL AND LeaseExpiresAtUtc IS NULL AND PublishedAtUtc IS NULL) OR
            (PublishStatus = 1 AND LeaseId IS NOT NULL AND LeaseExpiresAtUtc IS NOT NULL AND PublishedAtUtc IS NULL) OR
            (PublishStatus = 2 AND LeaseId IS NULL AND LeaseExpiresAtUtc IS NULL AND PublishedAtUtc IS NOT NULL)
    ),
    INDEX IX_ZoneEventRelayOutbox_Claim NONCLUSTERED
        (SourceShardId, PublishStatus, NextAttemptAtUtc, LeaseExpiresAtUtc, OutboxId),
    INDEX IX_ZoneEventRelayOutbox_PublishedRetention NONCLUSTERED
        (PublishStatus, PublishedAtUtc, OutboxId)
);

CREATE TABLE runtime.WorldInbox
(
    InboxId              BIGINT IDENTITY (1,1) NOT NULL,
    OutboxId             BIGINT                NOT NULL,
    AuthenticatedSource  NVARCHAR(128)         NOT NULL,
    SourceShardId        TINYINT               NOT NULL,
    SourceSequence       BIGINT                NOT NULL,
    DestinationShardId   TINYINT               NOT NULL,
    PayloadCategory      TINYINT               NOT NULL,
    Payload              VARBINARY(4096)       NOT NULL,
    PayloadHash          BINARY(32)            NOT NULL,
    CorrelationId        UNIQUEIDENTIFIER      NOT NULL,
    IdempotencyKey       UNIQUEIDENTIFIER      NOT NULL,
    ReceivedAtUtc        DATETIME2(3)          NOT NULL,
    EffectCompletedAtUtc DATETIME2(3)          NULL,
    CONSTRAINT PK_WorldInbox PRIMARY KEY CLUSTERED (InboxId),
    CONSTRAINT UQ_WorldInbox_OutboxId UNIQUE NONCLUSTERED (OutboxId),
    CONSTRAINT UQ_WorldInbox_IdempotencyKey UNIQUE NONCLUSTERED (IdempotencyKey),
    CONSTRAINT UQ_WorldInbox_SourceSequence UNIQUE NONCLUSTERED
        (AuthenticatedSource, SourceShardId, SourceSequence, DestinationShardId),
    CONSTRAINT CK_WorldInbox_SourceSequence CHECK (SourceSequence > 0),
    CONSTRAINT CK_WorldInbox_DifferentShards CHECK (SourceShardId <> DestinationShardId),
    CONSTRAINT CK_WorldInbox_PayloadCategory CHECK (PayloadCategory BETWEEN 1 AND 6),
    CONSTRAINT CK_WorldInbox_PayloadLength CHECK (DATALENGTH(Payload) BETWEEN 1 AND 4096)
);

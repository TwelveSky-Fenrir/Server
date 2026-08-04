CREATE TABLE runtime.WorldInboxEffectOperation
(
    InboxId            BIGINT           NOT NULL,
    OutboxId           BIGINT           NOT NULL,
    DestinationShardId TINYINT          NOT NULL,
    OperationKey       UNIQUEIDENTIFIER NOT NULL,
    PayloadCategory    TINYINT          NOT NULL,
    PayloadHash        BINARY(32)       NOT NULL,
    AppliedAtUtc       DATETIME2(3)     NOT NULL
        CONSTRAINT DF_WorldInboxEffectOperation_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_WorldInboxEffectOperation PRIMARY KEY CLUSTERED (InboxId),
    CONSTRAINT UQ_WorldInboxEffectOperation_OperationKey UNIQUE NONCLUSTERED (OperationKey),
    CONSTRAINT UQ_WorldInboxEffectOperation_OutboxId UNIQUE NONCLUSTERED (OutboxId),
    CONSTRAINT FK_WorldInboxEffectOperation_Inbox FOREIGN KEY (InboxId) REFERENCES runtime.WorldInbox (InboxId),
    CONSTRAINT CK_WorldInboxEffectOperation_OutboxId CHECK (OutboxId > 0),
    CONSTRAINT CK_WorldInboxEffectOperation_PayloadCategory CHECK (PayloadCategory BETWEEN 1 AND 6)
);

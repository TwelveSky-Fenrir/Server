CREATE TABLE game.EconomyOperationLedger
(
    OperationId          UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_EconomyOperationLedger_OperationId DEFAULT NEWSEQUENTIALID(),
    CorrelationId        UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_EconomyOperationLedger_CorrelationId DEFAULT NEWSEQUENTIALID(),
    ActorAccountId       INT              NOT NULL,
    ActorCharacterId     INT              NULL,
    OperationKind        TINYINT          NOT NULL,
    Cause                TINYINT          NOT NULL,
    IdempotencyKeyHash   BINARY(32)       NOT NULL,
    Status               TINYINT          NOT NULL
        CONSTRAINT DF_EconomyOperationLedger_Status DEFAULT 0,
    CreatedAtUtc         DATETIME2(3)     NOT NULL
        CONSTRAINT DF_EconomyOperationLedger_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CompletedAtUtc       DATETIME2(3)     NULL,
    CONSTRAINT PK_EconomyOperationLedger PRIMARY KEY CLUSTERED (OperationId),
    CONSTRAINT FK_EconomyOperationLedger_ActorAccount FOREIGN KEY (ActorAccountId)
        REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_EconomyOperationLedger_ActorCharacter FOREIGN KEY (ActorCharacterId)
        REFERENCES game.Characters (CharacterId),
    CONSTRAINT CK_EconomyOperationLedger_OperationKind CHECK (OperationKind IN (1, 2, 3, 4, 5)),
    CONSTRAINT CK_EconomyOperationLedger_Cause CHECK (Cause IN (1, 2, 3, 4, 5)),
    CONSTRAINT CK_EconomyOperationLedger_Status CHECK (Status BETWEEN 0 AND 3),
    CONSTRAINT CK_EconomyOperationLedger_Completion CHECK
        ((Status = 0 AND CompletedAtUtc IS NULL) OR
         (Status IN (1, 2, 3) AND CompletedAtUtc IS NOT NULL)),
    CONSTRAINT UQ_EconomyOperationLedger_ActorAccount_IdempotencyKeyHash
        UNIQUE NONCLUSTERED (ActorAccountId, IdempotencyKeyHash),
    INDEX IX_EconomyOperationLedger_CorrelationId NONCLUSTERED (CorrelationId),
    INDEX IX_EconomyOperationLedger_Status_CreatedAtUtc NONCLUSTERED (Status, CreatedAtUtc)
        INCLUDE (ActorAccountId, ActorCharacterId, OperationKind, Cause)
);

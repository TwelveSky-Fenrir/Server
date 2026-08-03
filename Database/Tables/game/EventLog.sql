CREATE TABLE game.EventLog
(
    EventLogId        BIGINT IDENTITY (1,1) NOT NULL,
    EventCode         SMALLINT              NOT NULL,
    Category          TINYINT               NOT NULL,
    ActorAccountId    INT                   NULL,
    ActorCharacterId  INT                   NULL,
    TargetAccountId   INT                   NULL,
    TargetCharacterId INT                   NULL,
    ShardId           SMALLINT              NULL,
    DeltaMoney        BIGINT                NULL,
    DeltaBigMoney     BIGINT                NULL,
    ItemId            INT                   NULL,
    Quantity          INT                   NULL,
    Outcome           TINYINT               NULL,
    Payload           NVARCHAR(MAX)         NULL,
    CreatedAtUtc      DATETIME2(3)          NOT NULL
        CONSTRAINT DF_EventLog_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_EventLog PRIMARY KEY CLUSTERED (EventLogId),
    CONSTRAINT CK_EventLog_Category CHECK (Category BETWEEN 0 AND 63),
    INDEX IX_EventLog_CreatedAtUtc NONCLUSTERED (CreatedAtUtc) INCLUDE (EventCode, Category, Outcome, ActorAccountId, ActorCharacterId),
    INDEX IX_EventLog_Category_CreatedAtUtc NONCLUSTERED (Category, CreatedAtUtc) INCLUDE (EventCode, ActorAccountId, ActorCharacterId, TargetAccountId, TargetCharacterId, Outcome),
    INDEX IX_EventLog_ActorCharacterId NONCLUSTERED (ActorCharacterId, CreatedAtUtc) INCLUDE (Category, EventCode, DeltaMoney, ItemId, Quantity)
        WHERE ActorCharacterId IS NOT NULL
);

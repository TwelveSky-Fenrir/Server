CREATE TABLE game.MonsterMoneyGrantLedger
(
    CorrelationId   UNIQUEIDENTIFIER NOT NULL,
    CharacterId     INT              NOT NULL,
    AccountId       INT              NOT NULL,
    Amount          BIGINT           NOT NULL,
    AuditEventLogId BIGINT           NOT NULL,
    AppliedAtUtc    DATETIME2(3)     NOT NULL
        CONSTRAINT DF_MonsterMoneyGrantLedger_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_MonsterMoneyGrantLedger PRIMARY KEY CLUSTERED (CorrelationId),
    CONSTRAINT UQ_MonsterMoneyGrantLedger_AuditEventLogId UNIQUE NONCLUSTERED (AuditEventLogId),
    CONSTRAINT FK_MonsterMoneyGrantLedger_AuditEventLog FOREIGN KEY (AuditEventLogId)
        REFERENCES game.EventLog (EventLogId),
    CONSTRAINT CK_MonsterMoneyGrantLedger_CorrelationId CHECK
        (CorrelationId <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT CK_MonsterMoneyGrantLedger_CharacterId CHECK (CharacterId > 0),
    CONSTRAINT CK_MonsterMoneyGrantLedger_AccountId CHECK (AccountId > 0),
    CONSTRAINT CK_MonsterMoneyGrantLedger_Amount CHECK (Amount BETWEEN 1 AND 2000000000),
    INDEX IX_MonsterMoneyGrantLedger_CharacterId_AppliedAtUtc NONCLUSTERED (CharacterId, AppliedAtUtc)
        INCLUDE (Amount, CorrelationId)
);

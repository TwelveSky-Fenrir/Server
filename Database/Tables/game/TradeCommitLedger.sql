CREATE TABLE game.TradeCommitLedger
(
    LedgerId       BIGINT           NOT NULL IDENTITY (1, 1),
    TradeToken     UNIQUEIDENTIFIER NOT NULL,
    CharacterA     INT              NOT NULL,
    CharacterB     INT              NOT NULL,
    CommittedAtUtc DATETIME2(3)     NOT NULL
        CONSTRAINT DF_TradeCommitLedger_CommittedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TradeCommitLedger PRIMARY KEY CLUSTERED (LedgerId),
    CONSTRAINT UQ_TradeCommitLedger_TradeToken UNIQUE NONCLUSTERED (TradeToken)
);

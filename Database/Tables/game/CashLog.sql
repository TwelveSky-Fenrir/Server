CREATE TABLE game.CashLog
(
    CashLogId    INT IDENTITY (1,1) NOT NULL,
    AccountId    INT                NOT NULL,
    Delta        INT                NOT NULL,
    BalanceAfter INT                NOT NULL,
    Reason       TINYINT            NOT NULL
        CONSTRAINT DF_CashLog_Reason DEFAULT 0,
    ProductId    INT                NULL,
    CreatedAtUtc DATETIME2(3)       NOT NULL
        CONSTRAINT DF_CashLog_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_CashLog PRIMARY KEY CLUSTERED (CashLogId),
    CONSTRAINT FK_CashLog_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    INDEX IX_CashLog_Account NONCLUSTERED (AccountId, CreatedAtUtc DESC) INCLUDE (Delta, BalanceAfter, Reason, ProductId)
);

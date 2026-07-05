-- Append-only audit trail of every cash balance movement, written in the same transaction as the
-- debit/credit (replaces the legacy fire-and-forget UDP GL_001_BUY_CASH line to ts25gamelog).
-- Delta is signed (credit/debit); BalanceAfter snapshots the post-movement balance. ProductId is an
-- unenforced reference (not a FK), same reasoning as game.Gifts.ProductId.
CREATE TABLE game.CashLog
(
    CashLogId    INT IDENTITY(1,1) NOT NULL,
    AccountId    INT     NOT NULL,
    Delta        INT     NOT NULL,
    BalanceAfter INT     NOT NULL,
    Reason       TINYINT NOT NULL CONSTRAINT DF_CashLog_Reason DEFAULT 0,
    ProductId    INT NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CashLog_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_CashLog PRIMARY KEY CLUSTERED (CashLogId),
    CONSTRAINT FK_CashLog_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    INDEX        IX_CashLog_Account NONCLUSTERED (AccountId)
);

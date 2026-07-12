-- Append-only audit trail of every cash balance movement, written in the same transaction as the
-- debit/credit (replaces the legacy fire-and-forget UDP GL_001_BUY_CASH line to ts25gamelog).
-- Delta is signed (credit/debit); BalanceAfter snapshots the post-movement balance. ProductId is an
-- unenforced reference (not a FK), same reasoning as game.Gifts.ProductId.
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
    -- Cross-schema FK naming: see admin.Bans' own header comment for the FK_<ChildTable>_<TargetSchema>_<Role>
    -- convention.
    CONSTRAINT FK_CashLog_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    -- Preemptively shaped as the covering index a future usp_CashLog_GetByAccount will need -- same shape as
    -- game.GiftLog's own IX_GiftLog_Account (CreatedAtUtc DESC in the key satisfies an ORDER BY, INCLUDE
    -- carries every remaining column so the seek never key-lookups back to the clustered index). No such
    -- procedure exists yet: this table is currently write-only, one INSERT per cash movement inside
    -- usp_Cash_Credit/usp_Cash_CreditAndConsumeItem/usp_Cash_DebitAndGrantItem. Building the covering shape
    -- now costs nothing (no reader depends on the old bare-AccountId shape) and avoids shipping the same
    -- key-lookup-plus-sort gap a second time when that reader is eventually added.
    INDEX IX_CashLog_Account NONCLUSTERED (AccountId, CreatedAtUtc DESC) INCLUDE (Delta, BalanceAfter, Reason, ProductId)
);

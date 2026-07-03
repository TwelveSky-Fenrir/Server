-- Append-only audit trail of every cash balance movement (replaces the legacy GL_001_BUY_CASH line sent
-- over fire-and-forget UDP to ts25gamelog -- report 06 §4.3: real-money events deserve a SQL row written
-- IN THE SAME TRANSACTION as the debit/credit, not a text file that can silently drop). Modeled on
-- game.GiftLog: free-standing history (own IDENTITY, no FK back to a live row), no unique constraint on
-- AccountId -- an account legitimately accumulates many rows.
-- Delta is signed (positive = credit, negative = debit); BalanceAfter snapshots the post-movement
-- balance so the ledger is self-checking without replaying it. Reason is an app-defined TINYINT
-- category (same store-as-given convention as admin.Bans.Reason; Fenrir's C# enum owns the taxonomy,
-- e.g. item-mall purchase / GM grant / refund). ProductId optionally records the world.ItemMallProducts
-- row a purchase debit paid for -- an unenforced reference (not a FK) for the same reason as
-- game.Gifts.ProductId: the catalog row may be retired while its purchase history must survive.
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
